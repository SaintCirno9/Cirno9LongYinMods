《龙胤立志传》玩家特殊立绘 Mod (LongYinPlayerSpeSkeleton)

【功能说明】
1. 这个 Mod 会拦截玩家的 `HeroData.SetSkeletonGraphic(...)` 流程。
2. Mod 会直接拦截 `Resources.Load(...)`，在游戏自己加载 `GameData/SpeHeroData` 时缓存可用的特殊人物列表。
3. 缓存时只保留 `特殊skeleton=1` 的条目，并记录对应“名字 -> ID”映射。
4. 当界面要显示玩家立绘时，Mod 会按你填写的特殊人物名字在缓存中查找目标。
5. 当前支持两种渲染模式：
   - `Skeleton`：沿用原版特殊立绘链路，加载 `Skeleton/SpeHero/{解析出的特殊人物ID}/skeleton_SkeletonData`
   - `StaticImage`：手动隐藏原有 `SkeletonGraphic`，改为显示一张外部静态图片
6. 这个 Mod 是独立项目，不会修改 `LongYinCustomSpeHeroData`。

【安装说明】
1. 本 Mod 依赖 MelonLoader。
2. 将编译得到的 `LongYinPlayerSpeSkeleton.dll` 放进游戏根目录的 `Mods` 文件夹。

【使用方法】
1. 首次启动游戏后，Mod 会自动生成独立配置文件：
   - `UserData\LongYinPlayerSpeSkeleton\LongYinPlayerSpeSkeleton.cfg`
2. 打开这个配置文件。
3. 将 `PlayerSpeHeroName=` 改成你想借用的特殊人物名字，例如：
   - `PlayerSpeHeroName=白云天`
4. 如果你要测试静态图片模式，可以额外设置：
   - `RenderMode=1`
   - `StaticImagePath=Portraits\白云天.png`
5. 将图片放到：
   - `UserData\LongYinPlayerSpeSkeleton\Portraits\白云天.png`
6. 重新进入游戏后，玩家详情页等走 `SetSkeletonGraphic(...)` 的界面会尝试显示该特殊立绘或静态图片。

【配置项】
1. `Enabled=true`
   - 总开关。
2. `PlayerSpeHeroName=`
   - 玩家要借用的特殊人物名字。
   - 必须能在已捕获的 `GameData/SpeHeroData` 中找到同名条目。
   - 且该条目的 `特殊skeleton` 必须为 `1`。
   - 仅当 `RenderMode=0` 时有效。
3. `VerboseLog=false`
   - 是否输出详细日志。
   - 打开后会在控制台打印当前读取到的配置值，以及玩家立绘替换成功日志。
4. 创建人物界面的“特殊立绘”勾选框
   - 这是游戏内开关，只影响当前游戏会话，不再写入配置文件。
5. `RenderMode=0`
   - `0`：继续使用当前原版托管链路，也就是原版人物动态立绘。
   - `1`：改为加载静态图片并手动替换 UI 立绘节点。
6. `StaticImagePath=Portraits\白云天.png`
   - 静态图片路径。
   - 可写绝对路径，也可写相对 `UserData\LongYinPlayerSpeSkeleton` 的路径。
7. `StaticImageHeroDetailInsetRatio=1.0`
   - 人物详情界面静态图的缩放比例。
8. `StaticImageHeroDetailOffsetX=0`
   - `StaticImageHeroDetailOffsetY=-90`
   - 人物详情界面静态图额外偏移。
9. `StaticImageCharacterCreationInsetRatio=1.0`
   - 创建人物界面静态图的缩放比例。
10. `StaticImageCharacterCreationOffsetX=0`
   - `StaticImageCharacterCreationOffsetY=-60`
   - 创建人物界面静态图额外偏移。
11. `StaticImageHudAvatarInsetRatio=0.8`
   - 左上角人物小头像静态图的缩放比例。
12. `StaticImageHudAvatarOffsetX=15`
   - `StaticImageHudAvatarOffsetY=-70`
   - 左上角人物小头像静态图额外偏移。
13. `StaticImageCardPortraitInsetRatio=0.9`
   - 卡片内肖像静态图的缩放比例。
14. `StaticImageCardPortraitOffsetX=-10`
   - `StaticImageCardPortraitOffsetY=-20`
   - 卡片内肖像静态图额外偏移。

【注意事项】
1. 如果当前还没捕获到 `GameData/SpeHeroData`，控制台会提示暂时无法按名字解析。
2. 如果你填写的特殊人物名字不存在于已捕获的可用列表中，控制台会提示原因。
3. 如果 `RenderMode=0` 且名字校验通过，但对应资源不存在，控制台会提示找不到：
   - `Skeleton/SpeHero/{ID}/skeleton_SkeletonData`
4. 如果 `RenderMode=1` 且图片不存在，控制台会提示当前图片路径无效，并回退显示原有立绘。
5. 当前版本优先覆盖 UI 用的 `SkeletonGraphic` 流程，先保证详情页这条链可用。
6. 如果后续你还想让大地图、战斗或别的 `SkeletonAnimation` 流程也替换，再继续补对应 patch 即可。
