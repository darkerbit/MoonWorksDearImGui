using System;
using System.Numerics;
using ImGuiNET;
using MoonWorks;
using MoonWorks.Graphics;

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

public class ImGuiGame : Game
{
	private readonly ImGuiRenderer _imRenderer;
	private readonly Texture _texture;
	
	public ImGuiGame(WindowCreateInfo windowCreateInfo, FrameLimiterSettings frameLimiterSettings,
		int targetTimestep = 60, bool debugMode = false) : base(windowCreateInfo, frameLimiterSettings, targetTimestep,
		debugMode)
	{
		var cb = GraphicsDevice.AcquireCommandBuffer();
		_imRenderer = new ImGuiRenderer(GraphicsDevice, cb, MainWindow);
		_texture = Texture.LoadPNG(GraphicsDevice, cb, "Content/Example.png");
		GraphicsDevice.Submit(cb);
	}
	
	protected override void Update(TimeSpan delta)
	{
		_imRenderer.Update(Inputs);
	}

	protected override void Draw(double alpha)
	{
		_imRenderer.NewFrameDraw(alpha);
		ImGui.NewFrame();
		
		ImGui.ShowDemoWindow();

		if (ImGui.Begin("Example texture"))
		{
			ImGui.Image(_imRenderer.BindTexture(_texture), new Vector2(500, 400));
		}
		ImGui.End();
		
		ImGui.Render();
		
		var cb = GraphicsDevice.AcquireCommandBuffer();
		var swapchainTexture = cb.AcquireSwapchainTexture(MainWindow);
		
		_imRenderer.BuildBuffers(ImGui.GetDrawData(), GraphicsDevice, cb);
		
		cb.BeginRenderPass(new ColorAttachmentInfo(swapchainTexture, Color.CornflowerBlue));
		_imRenderer.Render(cb);
		cb.EndRenderPass();
		
		GraphicsDevice.Submit(cb);
	}
}
