MailFilter

功能：
- 按信件类型过滤 NPC 主动发给主角的自动来信。
- 当前默认仅直接拦截“仇敌和解邀约信”。
- 支持按“信件类别 + 发信人与主角的关系阈值”共同决定是否拦截。
- 打开信件面板后，会在左侧增加“过滤”按钮，可直接在游戏内调整过滤规则。
- 被玩家设为“星标/收藏”的角色来信始终不拦截。
- 系统信、没有明确发信人的剧情信、未识别类型的邮件不会被过滤。

当前可识别的自动来信类型：
- 前任问罪信
- 仇敌和解邀约信
- 仇家踪迹求援信
- 贼人袭击求援信
- 入狱求探望信
- 门派宴会邀请信
- 退隐告别信
- 门派领地告急信
- 友好门派求援信
- 小聚邀约信
- 探宝邀约信
- 偷秘籍邀约信
- 秘籍交换信
- 武学心得小聚信
- 手头宽裕小聚信

各类型对应的信笺正文（模板）：
- 以下正文来自 `GameAssembly.dll` 对应的 `global-metadata.dat`，其中 `#PlayerName#`、`#PlayerForceName#`、`#TargetInteractName#` 以及 `{0}` ~ `{3}` 为游戏运行时替换占位符。
- 前任问罪信：`听说#PlayerName#近日得了新欢，难怪对我爱搭不理起来。这个月若是不来{0}寻我解释一番，可别怪我翻脸不认人。`
- 仇敌和解邀约信：`冤家宜解不宜结，若#PlayerName#本月得空，还望能来{0}与吾澄清误会。`
- 仇家踪迹求援信：`我在{0}寻得仇家踪迹，若#PlayerName#能在{2}日内赶来助阵，必当感激不尽！`
- 贼人袭击求援信：`我在{0}遭到贼人袭击，若#PlayerName#能在{2}日内赶来助阵，必当感激不尽！`
- 入狱求探望信：`#PlayerName#，说来惭愧。近日我一时失手，被{0}抓入狱中，眼下正关押在{1}。你若得空不妨来探望一番，也好一解我困坐囹圄之苦。`
- 门派宴会邀请信：`本月#PlayerForceName#将在门派祠堂中举办一场宴会，若#PlayerName#得空，不妨前来与师兄弟们一聚。`
- 退隐告别信：`#PlayerName#，我近来心生退隐江湖之意，只是此次一别恐怕再难相见。还望本月之内，#PlayerName#能来与吾告别，也不枉我俩往日一番情谊。`
- 门派领地告急信：`本门<b>{0}</b>遭敌对门派{1}袭击，危在旦夕，{2}`。其中 `{2}` 目前已确认可能为：`望掌门亲往协助防御！`、`望掌门亲往指挥防御！`、`还望各路门客尽快前去支援！`
- 友好门派求援信：`友好门派{3}治下<b>{0}</b>遭敌对门派{1}袭击，危在旦夕，不得已向本门求助。{2}`。其中 `{2}` 目前已确认可能为：`望掌门亲往协助防御！`、`现令各部弟子速速前去支援！`、`还望各路门客尽快前去支援！`
- 小聚邀约信：`我近日{1}，若#PlayerName#闲来无事，还望能到{0}与吾小聚一番。`
- 探宝邀约信：`我近日探得一处宝藏消息，奈何势单力孤，难以成行。若#PlayerName#能来{0}助我一臂之力，这宝藏咱们平分便是。`
- 偷秘籍邀约信：`久闻{1}武学高深莫测，江湖中人皆欲一睹为快，吾亦心向往之久矣。若#PlayerName#也有同感，不妨来{0}共商大计。`。这段文本写得比较含蓄，实际流程对应的是邀请主角一起去偷他人秘籍。
- 秘籍交换信：`前日因缘巧合下得到一{1}秘籍，可惜其路数与我所修武学不符，故闲置至今。若#PlayerName#手头也有多余秘籍，不妨来{0}与我交换一二。`
- 武学心得小聚信：`我近日忽有武学心得，若#PlayerName#闲来无事，还望能到{0}与吾小聚一番。`
- 手头宽裕小聚信：`我近日手头颇有盈余，若#PlayerName#闲来无事，还望能到{0}与吾小聚一番。`

安装：
1. 确保游戏已安装 MelonLoader。
2. 将 `MailFilter.dll` 放到游戏 `Mods` 目录。

配置：
1. 启动一次游戏后，会生成独立配置文件 `UserData\MailFilter.json`。
2. 可用开关如下：
   - `EnableFilter`
   - `BlockPreloverJealousy`
   - `BlockHaterReconcile`
   - `BlockHaterAidRequest`
   - `BlockBanditAidRequest`
   - `BlockPrisonVisitInvitation`
   - `BlockSectBanquetInvitation`
   - `BlockRetirementFarewell`
   - `BlockSectDefenseRequest`
   - `BlockFriendlyForceAidRequest`
   - `BlockGenericVisitInvitation`
   - `BlockTreasureInvitation`
   - `BlockMartialArtsDiscussion`
   - `BlockBookExchange`
   - `BlockInsightVisitInvitation`
   - `BlockWealthVisitInvitation`
   - `VerboseLogging`
3. 每类邮件还可以单独配置关系阈值：
   - `PreloverJealousyKeepAtOrAboveRelation`
   - `HaterReconcileKeepAtOrAboveRelation`
   - `HaterAidRequestKeepAtOrAboveRelation`
   - `BanditAidRequestKeepAtOrAboveRelation`
   - `PrisonVisitInvitationKeepAtOrAboveRelation`
   - `SectBanquetInvitationKeepAtOrAboveRelation`
   - `RetirementFarewellKeepAtOrAboveRelation`
   - `SectDefenseRequestKeepAtOrAboveRelation`
   - `FriendlyForceAidRequestKeepAtOrAboveRelation`
   - `GenericVisitInvitationKeepAtOrAboveRelation`
   - `TreasureInvitationKeepAtOrAboveRelation`
   - `MartialArtsDiscussionKeepAtOrAboveRelation`
   - `BookExchangeKeepAtOrAboveRelation`
   - `InsightVisitInvitationKeepAtOrAboveRelation`
   - `WealthVisitInvitationKeepAtOrAboveRelation`
4. 当前默认仅 `BlockHaterReconcile=true`，其余类型默认不拦截。
5. 当前默认所有关系阈值都是 `AlwaysBlock`，也就是该类型一旦启用就按旧逻辑直接拦截。
6. 如果想实现“只拦截关系低于友人的这类信件”，把对应类型的 `...KeepAtOrAboveRelation` 改成 `Friend` 即可。
7. 当前默认 `VerboseLogging=false`，需要时可在配置文件或游戏内面板里手动开启。
8. 本 Mod 不使用 `MelonPreferences.cfg`，避免与其他 Mod 共用同一配置文件。

关系层级：
- `Hater`
- `None`
- `Friend`
- `TeacherStudent`
- `Brother`
- `Relative`
- `Prelover`
- `Lover`

过滤规则：
- `AlwaysBlock`：该邮件类型启用后，不看关系，直接拦截。
- 其他关系值：保留“该关系及以上”的来信，拦截更低关系的来信。
- 例如设为 `Friend` 时，会拦截 `Hater` 和 `None`，保留 `Friend` 及以上。
- 游戏内面板使用左右箭头切换状态，顺序为：`不拦截 -> 直接拦截 -> 保留>=无特殊关系 -> ... -> 保留>=恋人`。

说明：
1. 本 Mod 的拦截点是 `GameController.GetNewMail`，因此会影响走统一收信入口的常规 NPC 来信。
2. 当前识别规则已覆盖 `ManageMonthTask` 里的常规月信、`AIController.SetAIStuff(aiStuffType == 13)` 生成的两类求援信、NPC 入狱后发来的探望信，以及部分门派事务/势力攻防类自动邮件。
3. 游戏内配置入口位于“信笺”页签打开后的左侧，点击“过滤”即可展开面板，按 `Esc` 可直接关闭面板。
4. 关系判定优先级为：`恋人 > 前任 > 亲属 > 结义 > 师徒 > 友人 > 仇敌 > 无特殊关系`。
5. `HeroData` 中还存在 `Relatives` 字段，但游戏现有 `GetRelationShipText` 并未直接展示“亲属”文本；本 Mod 仍会把它作为可过滤关系处理。
6. 当前版本只拦截邮件本身，不处理邮件伴随的大地图遭遇事件。
7. 被玩家设为“星标/收藏”的角色来信一律不拦截。
8. 如果后续发现新的自动来信模板，可以继续补充识别规则。
9. 部分门派事务邮件的标题可能是固定称谓，例如“掌门亲启”，这类邮件无法稳定解析具体发信人时，会按“无特殊关系”参与过滤。

发布文件：
1. DLL：`MailFilter.dll`
2. 压缩包：`MailFilter-v1.0.1.zip`
