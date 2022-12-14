using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ImGuiNET;
using MoonWorks;
using MoonWorks.Graphics;
using MoonWorks.Input;
using MoonWorks.Math.Float;
using SDL2;
using Buffer = MoonWorks.Graphics.Buffer;

/*
 * Copyright (c) 2022 darkerbit
 * 
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 * 
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would be
 *    appreciated but is not required.
 * 2. Altered source versions must be plainly marked as such, and must not be
 *    misrepresented as being the original software.
 * 3. This notice may not be removed or altered from any source distribution.
 * 
 */

namespace MoonWorksDearImGui;

public class ImGuiMoonWorksBackend
{
	[StructLayout(LayoutKind.Sequential)]
	private readonly struct ImGuiVert
	{
		public readonly Vector2 Position;
		public readonly Vector2 Uv;
		public readonly Color Col;
	}

	private readonly GraphicsDevice _gd;

	private readonly ShaderModule _vertShader;
	private readonly ShaderModule _fragShader;

	private Texture? _inbuiltTexture;

	private GraphicsPipeline? _pipeline;

	private Buffer _vertBuf;
	private Buffer _idxBuf;

	private ImDrawDataPtr _data;

	private readonly Sampler _sampler;

	private Matrix4x4 _proj;

	private static readonly Dictionary<IntPtr, Texture> Textures = new Dictionary<nint, Texture>();

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate string GetClipboardDelegate(IntPtr userData);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate void SetClipboardDelegate(IntPtr userData, string text);

	private static readonly GetClipboardDelegate GetClipboardFn = GetClipboard;
	private static readonly SetClipboardDelegate SetClipboardFn = SetClipboard;

	public ImGuiMoonWorksBackend(GraphicsDevice gd, CommandBuffer cb, Window window) : this(gd, cb,
		window.SwapchainFormat, new Vector2(window.Width, window.Height))
	{
	}

	public ImGuiMoonWorksBackend(GraphicsDevice gd, CommandBuffer cb, Window window, ShaderModule vertShader,
		ShaderModule fragShader) : this(gd, cb,
		window.SwapchainFormat, new Vector2(window.Width, window.Height), vertShader, fragShader)
	{
	}

	public ImGuiMoonWorksBackend(GraphicsDevice gd, CommandBuffer cb, TextureFormat format, Vector2 size)
		: this(gd, cb, format, size,
			new ShaderModule(gd, "Content/Shaders/SPIR-V/ImGui.vert.spv"),
			new ShaderModule(gd, "Content/Shaders/SPIR-V/ImGui.frag.spv")
		)
	{
	}

	public ImGuiMoonWorksBackend(GraphicsDevice gd, CommandBuffer cb, TextureFormat format, Vector2 size,
		ShaderModule vertShader, ShaderModule fragShader)
	{
		_gd = gd;

		Inputs.TextInput += TextInput;

		nint ctx = ImGui.CreateContext();
		ImGui.SetCurrentContext(ctx);

		Resize(size);

		_vertShader = vertShader;
		_fragShader = fragShader;

		_sampler = new Sampler(gd, SamplerCreateInfo.PointClamp);

		_vertBuf = Buffer.Create<ImGuiVert>(gd, BufferUsageFlags.Vertex, 1024 * 4);
		_idxBuf = Buffer.Create<ushort>(gd, BufferUsageFlags.Index, 1024 * 6);

		ImGuiIOPtr io = ImGui.GetIO();
		io.GetClipboardTextFn = Marshal.GetFunctionPointerForDelegate(GetClipboardFn);
		io.SetClipboardTextFn = Marshal.GetFunctionPointerForDelegate(SetClipboardFn);

		io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
		io.Fonts.AddFontDefault();
		UploadInbuiltTexture(cb);

		BuildPipeline(gd, format);
	}

	/// <summary>
	///     Starts a new ImGui frame and updates I/O.
	/// </summary>
	/// <remarks>
	///     Call inside <see cref="Game.Update" />.
	/// </remarks>
	/// <param name="inputs">Current input state</param>
	/// <param name="delta">Delta time</param>
	public void NewFrame(Inputs inputs, TimeSpan delta)
	{
		ImGuiIOPtr io = ImGui.GetIO();

		io.DeltaTime = (float) delta.TotalSeconds;

		io.AddMousePosEvent(inputs.Mouse.X, inputs.Mouse.Y);
		io.AddMouseWheelEvent(0, inputs.Mouse.Wheel);

		if (inputs.AnyPressed)
		{
			io.AddKeyEvent(ImGuiKey.ModCtrl,
				inputs.Keyboard.IsDown(KeyCode.LeftControl) || inputs.Keyboard.IsDown(KeyCode.RightControl));
			io.AddKeyEvent(ImGuiKey.ModShift,
				inputs.Keyboard.IsDown(KeyCode.LeftShift) || inputs.Keyboard.IsDown(KeyCode.RightShift));
			io.AddKeyEvent(ImGuiKey.ModAlt,
				inputs.Keyboard.IsDown(KeyCode.LeftAlt) || inputs.Keyboard.IsDown(KeyCode.RightAlt));
			io.AddKeyEvent(ImGuiKey.ModSuper,
				inputs.Keyboard.IsDown(KeyCode.LeftMeta) || inputs.Keyboard.IsDown(KeyCode.RightMeta));
		}

		if (inputs.Mouse.LeftButton.IsPressed || inputs.Mouse.LeftButton.IsReleased)
		{
			io.AddMouseButtonEvent(0, inputs.Mouse.LeftButton.IsDown);
		}

		if (inputs.Mouse.RightButton.IsPressed || inputs.Mouse.RightButton.IsReleased)
		{
			io.AddMouseButtonEvent(1, inputs.Mouse.RightButton.IsDown);
		}

		if (inputs.Mouse.MiddleButton.IsPressed || inputs.Mouse.MiddleButton.IsReleased)
		{
			io.AddMouseButtonEvent(2, inputs.Mouse.MiddleButton.IsDown);
		}

		foreach (KeyCode key in Keys.Keys)
		{
			if (!inputs.Keyboard.IsPressed(key) && !inputs.Keyboard.IsReleased(key))
			{
				continue;
			}

			io.AddKeyEvent(Keys.GetValueOrDefault(key, ImGuiKey.None), inputs.Keyboard.IsDown(key));
			io.SetKeyEventNativeData(Keys.GetValueOrDefault(key, ImGuiKey.None), (int) key, (int) key);
		}
	}

	/// <summary>
	///     Builds the vertex and index buffers used for ImGui rendering.
	/// </summary>
	/// <remarks>
	///     Call after <see cref="ImGui.Render" /> and before <see cref="Render" />.
	///     Must not be called during a render pass.
	/// </remarks>
	/// <param name="data">ImGui draw data from <see cref="ImGui.GetDrawData" /></param>
	/// <param name="cb">Command buffer, must not have active render pass</param>
	public unsafe void BuildBuffers(ImDrawDataPtr data, CommandBuffer cb)
	{
		if (data.TotalVtxCount > _vertBuf.Size / sizeof(ImGuiVert))
		{
			_vertBuf.Dispose();
			_vertBuf = Buffer.Create<ImGuiVert>(_gd, BufferUsageFlags.Vertex, (uint) data.TotalVtxCount);
		}

		if (data.TotalIdxCount > _idxBuf.Size / sizeof(ushort))
		{
			_idxBuf.Dispose();
			_idxBuf = Buffer.Create<ushort>(_gd, BufferUsageFlags.Index, (uint) data.TotalIdxCount);
		}

		uint vtxOffset = 0;
		uint idxOffset = 0;

		for (int i = 0; i < data.CmdListsCount; i++)
		{
			ImDrawListPtr list = data.CmdListsRange[i];

			cb.SetBufferData<ImGuiVert>(_vertBuf, list.VtxBuffer.Data, vtxOffset, (uint) list.VtxBuffer.Size);
			cb.SetBufferData<ushort>(_idxBuf, list.IdxBuffer.Data, idxOffset, (uint) list.IdxBuffer.Size);

			vtxOffset += (uint) list.VtxBuffer.Size;
			idxOffset += (uint) list.IdxBuffer.Size;
		}

		_data = data;
	}

	/// <summary>
	///     Renders the Dear ImGui windows.
	/// </summary>
	/// <remarks>
	///     Call after <see cref="BuildBuffers" />, inside of a render pass.
	/// </remarks>
	/// <param name="cb">Command buffer, must have an active render pass</param>
	public void Render(CommandBuffer cb)
	{
		ImGuiIOPtr io = ImGui.GetIO();

		cb.BindGraphicsPipeline(_pipeline);
		uint vtxUniform = cb.PushVertexShaderUniforms(_proj);

		cb.BindVertexBuffers(_vertBuf);
		cb.BindIndexBuffer(_idxBuf, IndexElementSize.Sixteen);

		uint vtxOffset = 0;
		uint idxOffset = 0;

		for (int j = 0; j < _data.CmdListsCount; j++)
		{
			ImDrawListPtr list = _data.CmdListsRange[j];

			for (int i = 0; i < list.CmdBuffer.Size; i++)
			{
				ImDrawCmdPtr cmd = list.CmdBuffer[i];

				cb.BindFragmentSamplers(new TextureSamplerBinding(Lookup(cmd.TextureId), _sampler));
				cb.SetScissor(
					new Rect(
						(int) Math.Clamp(cmd.ClipRect.X, 0, io.DisplaySize.X),
						(int) Math.Clamp(cmd.ClipRect.Y, 0, io.DisplaySize.Y),
						(int) Math.Clamp(cmd.ClipRect.Z - cmd.ClipRect.X, 0, io.DisplaySize.X),
						(int) Math.Clamp(cmd.ClipRect.W - cmd.ClipRect.Y, 0, io.DisplaySize.Y)
					)
				);

				cb.DrawIndexedPrimitives(cmd.VtxOffset + vtxOffset, cmd.IdxOffset + idxOffset, cmd.ElemCount / 3,
					vtxUniform, 0);
			}

			vtxOffset += (uint) list.VtxBuffer.Size;
			idxOffset += (uint) list.IdxBuffer.Size;
		}
	}

	/// <summary>
	///     (Re)uploads the inbuilt ImGui texture.
	/// </summary>
	/// <remarks>
	///     Call after changing font settings.
	/// </remarks>
	/// <param name="cb">Command buffer, must not have active render pass</param>
	public void UploadInbuiltTexture(CommandBuffer cb)
	{
		ImGuiIOPtr io = ImGui.GetIO();

		io.Fonts.GetTexDataAsRGBA32(out IntPtr pixelPtr, out int width, out int height, out int bpp);

		_inbuiltTexture?.Dispose();
		_inbuiltTexture = Texture.CreateTexture2D(_gd, (uint) width, (uint) height, TextureFormat.R8G8B8A8,
			TextureUsageFlags.Sampler);

		cb.SetTextureData(_inbuiltTexture, pixelPtr, (uint) (width * height * bpp));

		io.Fonts.SetTexID(_inbuiltTexture.Handle);
	}

	/// <summary>
	///     Binds a texture to use in the renderer.
	/// </summary>
	/// <param name="texture">Texture</param>
	/// <returns>Texture handle to pass into ImGui</returns>
	public static IntPtr BindTexture(Texture texture)
	{
		Textures.TryAdd(texture.Handle, texture);
		return texture.Handle;
	}

	/// <summary>
	///     Unbinds a texture from the renderer.
	/// </summary>
	/// <param name="texture">Texture</param>
	public static void UnbindTexture(Texture texture)
	{
		Textures.Remove(texture.Handle);
	}

	/// <summary>
	///     Resizes ImGui viewport to the size of the given window.
	/// </summary>
	/// <param name="window">Window to resize to</param>
	public void Resize(Window window)
	{
		Resize(new Vector2(window.Width, window.Height));
	}

	/// <summary>
	///     Resizes ImGui viewport to a given size.
	/// </summary>
	/// <param name="size">Size of the viewport</param>
	public void Resize(Vector2 size)
	{
		_proj = Matrix4x4.CreateOrthographicOffCenter(0, size.X, size.Y, 0, -1.0f, 1.0f);

		ImGuiIOPtr io = ImGui.GetIO();
		io.DisplaySize = new System.Numerics.Vector2(size.X, size.Y);
	}

	private void BuildPipeline(GraphicsDevice gd, TextureFormat format)
	{
		GraphicsPipelineCreateInfo gpci = new GraphicsPipelineCreateInfo
		{
			AttachmentInfo = new GraphicsPipelineAttachmentInfo(
				new ColorAttachmentDescription(format, ColorAttachmentBlendState.NonPremultiplied)
			),
			DepthStencilState = DepthStencilState.Disable,
			MultisampleState = MultisampleState.None,
			PrimitiveType = PrimitiveType.TriangleList,
			RasterizerState = RasterizerState.CCW_CullNone,
			VertexInputState = new VertexInputState
			{
				VertexBindings = new[] { VertexBinding.Create<ImGuiVert>() },
				VertexAttributes = new[]
				{
					VertexAttribute.Create<ImGuiVert>("Position", 0),
					VertexAttribute.Create<ImGuiVert>("Uv", 1),
					VertexAttribute.Create<ImGuiVert>("Col", 2),
				},
			},
			VertexShaderInfo = GraphicsShaderInfo.Create<Matrix4x4>(_vertShader, "main", 0),
			FragmentShaderInfo = GraphicsShaderInfo.Create(_fragShader, "main", 1),
		};

		_pipeline = new GraphicsPipeline(gd, gpci);
	}

	private Texture Lookup(IntPtr handle)
	{
		return handle == _inbuiltTexture!.Handle ? _inbuiltTexture! : Textures[handle];
	}

	private static string GetClipboard(IntPtr userData)
	{
		return SDL.SDL_GetClipboardText();
	}

	private static void SetClipboard(IntPtr userData, string text)
	{
		SDL.SDL_SetClipboardText(text);
	}

	private static void TextInput(char c)
	{
		ImGui.GetIO().AddInputCharacter(c);
	}

	private static readonly Dictionary<KeyCode, ImGuiKey> Keys = new Dictionary<KeyCode, ImGuiKey>
	{
		[KeyCode.Tab] = ImGuiKey.Tab,
		[KeyCode.Left] = ImGuiKey.LeftArrow,
		[KeyCode.Right] = ImGuiKey.RightArrow,
		[KeyCode.Up] = ImGuiKey.UpArrow,
		[KeyCode.Down] = ImGuiKey.DownArrow,
		[KeyCode.PageUp] = ImGuiKey.PageUp,
		[KeyCode.PageDown] = ImGuiKey.PageDown,
		[KeyCode.Home] = ImGuiKey.Home,
		[KeyCode.End] = ImGuiKey.End,
		[KeyCode.Insert] = ImGuiKey.Insert,
		[KeyCode.Delete] = ImGuiKey.Delete,
		[KeyCode.Backspace] = ImGuiKey.Backspace,
		[KeyCode.Space] = ImGuiKey.Space,
		[KeyCode.Return] = ImGuiKey.Enter,
		[KeyCode.Escape] = ImGuiKey.Escape,
		[KeyCode.LeftControl] = ImGuiKey.LeftCtrl,
		[KeyCode.LeftShift] = ImGuiKey.LeftShift,
		[KeyCode.LeftAlt] = ImGuiKey.LeftAlt,
		[KeyCode.LeftMeta] = ImGuiKey.LeftSuper,
		[KeyCode.RightControl] = ImGuiKey.RightCtrl,
		[KeyCode.RightShift] = ImGuiKey.RightShift,
		[KeyCode.RightAlt] = ImGuiKey.RightAlt,
		[KeyCode.RightMeta] = ImGuiKey.RightSuper,
		[KeyCode.D0] = ImGuiKey._0,
		[KeyCode.D1] = ImGuiKey._1,
		[KeyCode.D2] = ImGuiKey._2,
		[KeyCode.D3] = ImGuiKey._3,
		[KeyCode.D4] = ImGuiKey._4,
		[KeyCode.D5] = ImGuiKey._5,
		[KeyCode.D6] = ImGuiKey._6,
		[KeyCode.D7] = ImGuiKey._7,
		[KeyCode.D8] = ImGuiKey._8,
		[KeyCode.D9] = ImGuiKey._9,
		[KeyCode.A] = ImGuiKey.A,
		[KeyCode.B] = ImGuiKey.B,
		[KeyCode.C] = ImGuiKey.C,
		[KeyCode.D] = ImGuiKey.D,
		[KeyCode.E] = ImGuiKey.E,
		[KeyCode.F] = ImGuiKey.F,
		[KeyCode.G] = ImGuiKey.G,
		[KeyCode.H] = ImGuiKey.H,
		[KeyCode.I] = ImGuiKey.I,
		[KeyCode.J] = ImGuiKey.J,
		[KeyCode.K] = ImGuiKey.K,
		[KeyCode.L] = ImGuiKey.L,
		[KeyCode.M] = ImGuiKey.M,
		[KeyCode.N] = ImGuiKey.N,
		[KeyCode.O] = ImGuiKey.O,
		[KeyCode.P] = ImGuiKey.P,
		[KeyCode.Q] = ImGuiKey.Q,
		[KeyCode.R] = ImGuiKey.R,
		[KeyCode.S] = ImGuiKey.S,
		[KeyCode.T] = ImGuiKey.T,
		[KeyCode.U] = ImGuiKey.U,
		[KeyCode.V] = ImGuiKey.V,
		[KeyCode.W] = ImGuiKey.W,
		[KeyCode.X] = ImGuiKey.X,
		[KeyCode.Y] = ImGuiKey.Y,
		[KeyCode.Z] = ImGuiKey.Z,
		[KeyCode.F1] = ImGuiKey.F1,
		[KeyCode.F2] = ImGuiKey.F2,
		[KeyCode.F3] = ImGuiKey.F3,
		[KeyCode.F4] = ImGuiKey.F4,
		[KeyCode.F5] = ImGuiKey.F5,
		[KeyCode.F6] = ImGuiKey.F6,
		[KeyCode.F7] = ImGuiKey.F7,
		[KeyCode.F8] = ImGuiKey.F8,
		[KeyCode.F9] = ImGuiKey.F9,
		[KeyCode.F10] = ImGuiKey.F10,
		[KeyCode.F11] = ImGuiKey.F11,
		[KeyCode.F12] = ImGuiKey.F12,
		[KeyCode.Apostrophe] = ImGuiKey.Apostrophe,
		[KeyCode.Comma] = ImGuiKey.Comma,
		[KeyCode.Minus] = ImGuiKey.Minus,
		[KeyCode.Period] = ImGuiKey.Period,
		[KeyCode.Slash] = ImGuiKey.Slash,
		[KeyCode.Semicolon] = ImGuiKey.Semicolon,
		[KeyCode.Equals] = ImGuiKey.Equal,
		[KeyCode.LeftBracket] = ImGuiKey.LeftBracket,
		[KeyCode.Backslash] = ImGuiKey.Backslash,
		[KeyCode.RightBracket] = ImGuiKey.RightBracket,
		[KeyCode.Grave] = ImGuiKey.GraveAccent,
		[KeyCode.CapsLock] = ImGuiKey.CapsLock,
		[KeyCode.ScrollLock] = ImGuiKey.ScrollLock,
		[KeyCode.NumLockClear] = ImGuiKey.NumLock,
		[KeyCode.PrintScreen] = ImGuiKey.PrintScreen,
		[KeyCode.Pause] = ImGuiKey.Pause,
		[KeyCode.Keypad0] = ImGuiKey.Keypad0,
		[KeyCode.Keypad1] = ImGuiKey.Keypad1,
		[KeyCode.Keypad2] = ImGuiKey.Keypad2,
		[KeyCode.Keypad3] = ImGuiKey.Keypad3,
		[KeyCode.Keypad4] = ImGuiKey.Keypad4,
		[KeyCode.Keypad5] = ImGuiKey.Keypad5,
		[KeyCode.Keypad6] = ImGuiKey.Keypad6,
		[KeyCode.Keypad7] = ImGuiKey.Keypad7,
		[KeyCode.Keypad8] = ImGuiKey.Keypad8,
		[KeyCode.Keypad9] = ImGuiKey.Keypad9,
		[KeyCode.KeypadPeriod] = ImGuiKey.KeypadDecimal,
		[KeyCode.KeypadDivide] = ImGuiKey.KeypadDivide,
		[KeyCode.KeypadMultiply] = ImGuiKey.KeypadMultiply,
		[KeyCode.KeypadMinus] = ImGuiKey.KeypadSubtract,
		[KeyCode.KeypadPlus] = ImGuiKey.KeypadAdd,
		[KeyCode.KeypadEnter] = ImGuiKey.Enter,
	};
}
