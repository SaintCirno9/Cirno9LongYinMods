CustomLoseVideo

功能：
1. 玩家战败后，Mod 会在游戏切回地图或场景音乐时自动播放自定义战败视频。
2. 视频以带边框的覆盖窗口播放，播放结束后自动关闭。
3. 视频窗口右上角有关闭按钮，可以提前结束播放。

安装：
1. 确保游戏已安装 MelonLoader。
2. 将 `Mods\CustomLoseVideo.dll` 放到游戏 `Mods` 目录。
3. 将配置和视频目录放到 `UserData\CustomLoseVideo`。
4. 默认视频路径为：
   `UserData\CustomLoseVideo\Video\FightLose.mp4`

配置：
1. 配置文件路径：
   `UserData\CustomLoseVideo\CustomLoseVideo.cfg`

2. `Enabled=true`
   是否启用整个 Mod。
   非法值会按默认值 `true` 处理。

3. `VerboseLogging=false`
   是否输出详细日志。
   非法值会按默认值 `false` 处理。

4. `VideoFile=Video\FightLose.mp4`
   相对于 `UserData\CustomLoseVideo` 的视频路径。
   空值会按默认值 `Video\FightLose.mp4` 处理。

5. `VideoWidthPercent=100`
   视频窗口宽度占屏幕宽度的百分比，范围 10-100。
   非法值会按默认值 `100` 处理，超出范围会限制到 10-100。

6. `VideoHeightPercent=100`
   视频窗口高度占屏幕高度的百分比，范围 10-100。
   非法值会按默认值 `100` 处理，超出范围会限制到 10-100。

视频格式：
1. 推荐使用 `mp4`。
2. 推荐编码为 H.264 视频和 AAC 音频。

发布文件：
1. DLL：`Mods\CustomLoseVideo.dll`
2. 配置：`UserData\CustomLoseVideo\CustomLoseVideo.cfg`
3. 视频目录：`UserData\CustomLoseVideo\Video`
