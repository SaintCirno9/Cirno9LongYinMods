InfoTabRightSide

作者：
- SaintCirno9

功能：
1. 将 `InfoController.AddInfoTab` 生成的弹出提示列移动到屏幕右侧。
2. 将新提示的入场方向改为从右向左滑入。
3. 提供简单偏移配置，方便按个人分辨率和 UI 习惯微调位置。

安装：
1. 确保游戏已经安装 MelonLoader。
2. 将 `InfoTabRightSide.dll` 放到游戏根目录的 `Mods` 文件夹。
3. 首次启动游戏后，mod 会自动生成配置文件。

配置：
1. 配置文件路径：`UserData\InfoTabRightSide.cfg`
2. 如果配置文件不存在，mod 启动后会自动生成默认配置。
3. 配置项如下：
- `Enabled=true`
- `OffsetX=-80`
- `OffsetY=0`
4. `Enabled` 控制是否启用右侧提示列和右侧滑入动画，默认值 `true`。
5. `OffsetX` 控制整体水平偏移，允许填任意数字，默认值 `-80`；正数向右，负数向左。
6. `OffsetY` 控制整体垂直偏移，允许填任意数字，默认值 `0`；正数向上，负数向下。
7. 任何缺失项、未知项或非法值都会回退到默认值，并自动重写配置文件。

发布文件：
1. DLL：`InfoTabRightSide.dll`
2. 压缩包：`InfoTabRightSide-v1.0.0.zip`
