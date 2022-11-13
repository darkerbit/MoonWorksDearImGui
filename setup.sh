#!/bin/bash

# Exit immediately on error
set -e

mkdir -p natives
cd natives

echo -e "\e[32mDownloading moonlibs...\e[m"
curl https://moonside.games/files/moonlibs.tar.bz2 > moonlibs.tar.bz2

echo -e "\e[32mExtracting moonlibs...\e[m"
tar -xf moonlibs.tar.bz2

cd ..

echo -e "\e[32mCopying ImGui.NET natives...\e[m"
echo -e "\e[32mIf this fails, you forgot to grab submodules!\e[m"
cp lib/ImGui.NET/deps/cimgui/win-x64/cimgui.dll natives/windows/
cp lib/ImGui.NET/deps/cimgui/linux-x64/cimgui.so natives/lib64/

echo -e "\e[32mDone!\e[m"
