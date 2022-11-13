﻿using MoonWorks;

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

public static class Program
{
	public static void Main()
	{
		var wci = new WindowCreateInfo
		{
			WindowTitle = "MoonWorks Dear ImGui",
			WindowWidth = 1280,
			WindowHeight = 720,
			PresentMode = PresentMode.FIFO,
			ScreenMode = ScreenMode.Windowed,
			StartMaximized = false,
			SystemResizable = false,
		};

		var fls = new FrameLimiterSettings
		{
			Cap = 0,
			Mode = FrameLimiterMode.Uncapped,
		};

		var game = new ImGuiGame(wci, fls, 60, true);
		game.Run();
	}
}
