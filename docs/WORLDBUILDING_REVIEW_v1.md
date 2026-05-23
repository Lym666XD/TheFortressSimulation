# 世界观与软背景审阅报告 v1

**版本**: 1.0
**编写人**: Claude (Opus 4.7)
**日期**: 2026-05-24
**用户定位决策**:
- 时代轴: **古典（罗马/希腊）→ 文艺复兴早期**（不进入科学革命与工业革命）
- 奇幻浓度: **中低魔法**，经典神话生物 + 神明体系 + **轻量**克苏鲁/北欧/阿拉伯/凯尔特元素
- 与 DF 关系: **吸收 DF 已有内容**（生物部位、神器、地穴文明、传说模式），**补充 DF 没有的或处理粗糙的部分**（玻璃、化学、魔法体系、教派、克苏鲁式禁忌、伊斯兰/北欧/凯尔特题材）

本报告专门回答用户的问题 #1（科技树合身度 + 缺漏 + 头脑风暴），为后续问题 #2（civ/文化/宗教等软系统补漏）提供基线。

---

## 0) TL;DR

- **现状**: 已有 8 条产业链文档（农业、畜牧、林业、皮革、酿造、纺织、烹饪、造纸印书）+ 1 份综合 PROCESS_CHAIN（含建筑/石作/金属/火器）。形式优雅、风格统一（C/M/R 三段台阶、数据驱动、与 DF 对齐）。
- **科技树合身度**: **整体合身，少量越界**。「文艺复兴早期」上限基本被守住，但有几处轻度越界（Stationer's Company 1557 → 已经是中后期；Maximilian 板甲风格 → 16 世纪盛期，但作为艺术风格可保留）。**真正的问题是科技树宽度不够**，约 12–15 条同时代关键产业链尚未建立。
- **软背景**: **几乎完全空白**。CREATURE_SPEC / MATERIALS_SPEC 已为未来的魔法/特质/价值/宗教挂了钩（`mana_conductivity`、`traits`、`values`、`factions`、`mythic gates magical content`），但没有任何专门的**世界观/文明/宗教/魔法/神话生物**规范文档。`/docs/` 下的两份 PDF（Magic & Occultism、Fantasy Creatures）含有大量原料但**尚未被结构化吸收**到设计文档体系中。
- **头脑风暴建议**: 报告 §5–§7 给出 16 条产业链补漏 + 6 大软背景体系新增（魔法、神话生物、文明、宗教、文化、克苏鲁式禁忌）+ 8 个可立即起草的新 SPEC 文档名称。

---

## 1) 现有 8 条产业链评估表（科技树合身度）

> 评分基准：★ = 单独打分，5★ = 完美贴合"古典→文艺复兴早期 + 中低魔法"定位；问题列只标真问题，体例小毛病不列。

| 文档 | 时代覆盖 | 与定位合身度 | 主要问题 | 是否需要返工 |
|---|---|---|---|---|
| Forestry_NavalTimber_Design_v1 | C/M/R | ★★★★★ | 无；水力锯 R 期延后到"deferred"是正确的——避免越界 | 否 |
| Agriculture_Design_v1 | C/M/R | ★★★★★ | 显式声明"不引入哥伦布大交流物种" → 与"R 时代纺织引入新大陆胭脂虫"轻微不一致（见纺织条） | 微调（统一物种引入策略） |
| Husbandry_Design_v1 | C/M/R | ★★★★★ | R 期"配合饲料"已经接近近代畜牧学边缘，但仍在合理范围 | 否 |
| Leatherwork_DF_Simplified_bilingual | C/M/R | ★★★★☆ | "Cordovan"实际为 7–14 世纪西班牙（盛行至 M），放 R 期略偏后；问题不大 | 否 |
| Brewing_Industry_Design_Bilingual | C/M/R | ★★★★☆ | "Reinheitsgebot 1516" 严格说是 R 早期末尾，但完全可保留 | 否 |
| Textile_Industry_Research_and_Design_CN | C/M/R | ★★★★☆ | 新大陆胭脂虫（16 世纪）正好压线，但与农业声明的"不引入哥伦布作物"冲突 | 微调（要么纺织也只用旧大陆 kermes/茜草；要么农业开个"贸易输入"豁免） |
| Cooking_System_Design | C/M/R | ★★★★★ | 干净利落；糖在 R 期出现合理（地中海甘蔗 14–15 世纪已有，蔗糖到 R 期普及） | 否 |
| Papermaking_and_Book_Industry_Design_CN | C/M/R | ★★★★☆ | Stationers' Company 是 1557，已经偏后；可以保留作为"R 末"的成就性内容，或下放为"印刷所"内置的可选规则 | 微调 |
| CHATGPT_PROCESS_CHAIN（综合，含建筑/石/金/火器） | C/M/R | ★★★★☆ | (1) Maximilian 板甲是 1500s 盛期艺术风格，作为"装饰风"无问题；(2) Saker 野战炮 + 迫击炮 + 卡利弗 + Musket 已经是 16 世纪中后期英国—西班牙战争年代，**接近上限**；如果坚持"R 早期"，建议把 Mortar 和 Musket 留作可选 R 末解锁 | 微调 |

**结论**: 8 条产业链 + 综合工坊文档**整体非常优秀**，体例统一、机制轻量、与 DF 对接清晰、可直接落库。**没有任何一条需要重写**，只需要 3–4 处微调（见 §4）。

---

## 2) 科技树合身度问题（时代越界 / 时代不平衡）

### 2.1 越界条目（需要决策）

| 项目 | 出处 | 历史年代 | 当前 era 标 | 建议 |
|---|---|---|---|---|
| Mortar 迫击炮 | PROCESS_CHAIN 火器 | 1450s 围攻战出现，16 世纪定型 | R | 保留，但作为 **R 末解锁**（解锁条件：玩家已建造印刷所或 Saker） |
| Musket 火绳火枪（长重型） | PROCESS_CHAIN 火器 | 1521 西班牙 → 1567 法国制式 | R | 保留 R，但限定数量 |
| Stationers' Company | 造纸印刷 | 1557 | R | 改名为 **行业登记台**（去除真实公会名），保留机制 |
| 新大陆胭脂虫 cochineal | 纺织 | 1520+ 西班牙引入 | R | 二选一：A) 改用旧大陆 kermes 替代；B) 在农业/贸易文档开"贸易输入"白名单豁免 |
| 复杂半板甲 / Maximilian 板甲 | PROCESS_CHAIN 金属 | 1500–1530 | R | 保留为**风格标签**而非生产线门槛 |

### 2.2 古典时代偏弱

C 期内容多为"中世纪工艺的简化前身"，没有充分表达**罗马—希腊文明的独特感**。建议：

- 加入 **Roman concrete (opus caementicium)** 配方（已经在建筑业 Set C3 出现，可强化）
- **Hypocaust 罗马地暖** → 建筑业可选"地中海宅邸"附件
- **Aqueduct & Bath** → 与未来的水力/卫生系统挂钩
- **Garum 鱼酱** → 已经在烹饪 C1/C3 命中，OK
- **羊皮纸 vs 纸草** → 已经在造纸 C 命中，OK
- **战车 chariot / 三段桨座船 trireme** → 战斗/海军相关，留待后期
- **希腊—罗马多神祠庙** → 留待宗教 spec

C 期需要补的核心**情绪锚点**: 罗马法、罗马道路、神祕教派、雅典市集、奥林匹斯神殿、戴奥尼索斯酒祭、密特拉教 Mithraism——这些是**软背景**，不是产业，但会显著拉开"时代质感"。

### 2.3 文艺复兴早期边界（明确禁止线）

为避免后续散乱越界，建议明确以下**禁止技术**作为时代封顶：

- ❌ 蒸汽机（Newcomen 1712 / Watt 1769）
- ❌ 燧发枪 flintlock（1610s 之后）
- ❌ Bessemer 转炉炼钢（1856）
- ❌ 电气 / 化学合成染料 / 化肥
- ❌ 显微镜（1590s 边缘）/ 望远镜（1608 边缘）— 严格"早期"应该不要
- ❌ 资本主义复式记账法的全面落地（Pacioli 1494 边缘）— 可作为高阶贸易系统的隐性背景，不要做成游戏机制
- ❌ 远洋帆船（Caravel 已经 R 早期，galleon 严格说 R 中）— 海军限定在桨帆 / 早期 Carrack

边界明确后，可以放心地把以下技术作为**明确许可**：

- ✅ 印刷活字（1450）
- ✅ Reinheitsgebot 啤酒纯净法（1516 边缘——可保留作为"R 末"高阶解锁）
- ✅ 早期火药武器（卡利弗、火绳枪、Saker、Mortar）
- ✅ 早期文艺复兴绘画/雕塑/复音音乐 → 作为艺术系统的灵感来源
- ✅ 早期数学/几何/制图学 → 作为建筑师/工程师职业的灵感
- ✅ Paracelsus 1493–1541 → **正好压线**，他的炼金/医药/三元素学说是整个"魔法/化学"挂钩的最佳锚点

---

## 3) 现有产业链覆盖 vs 缺漏清单（按优先级排序）

### 3.1 已覆盖（8 大主流 + 1 综合 = 涉及到的产业链）

| 已覆盖 | 当前文档 |
|---|---|
| 农业 | Agriculture_Design_v1 |
| 畜牧/家禽 | Husbandry_Design_v1 |
| 林业 + 木材分级 | Forestry_NavalTimber_Design_v1 |
| 皮革 | Leatherwork_DF_Simplified_bilingual |
| 酿造（啤酒/葡萄酒/蒸馏/醋） | Brewing_Industry_Design_Bilingual |
| 纺织（毛/麻/丝/染整） | Textile_Industry_Research_and_Design_CN |
| 烹饪/保藏 | Cooking_System_Design |
| 造纸/印刷/装订 | Papermaking_and_Book_Industry_Design_CN |
| 建筑/砖瓦/石灰/混凝土 | Building_Industry_Simplified_Design |
| 石作（家具/雕像/铭刻） | CHATGPT_PROCESS_CHAIN §石材工坊 |
| 金属（构件/工具/武器/护甲） | CHATGPT_PROCESS_CHAIN §金属工坊 |
| 火器装配 | CHATGPT_PROCESS_CHAIN §火器工坊 |

### 3.2 关键缺漏（按优先级，1 = 最急）

| 优先级 | 缺漏产业 | 重要性 | 当前的隐式引用（已经被假定存在） |
|---|---|---|---|
| **P0** | **采矿 Mining**（地表/地下/矿脉/工具） | 整个金属/石/化工链的源头 | MININGSYSTEM_SPEC 有引擎层 spec，但**没有"采矿业 C/M/R 产业链"文档** |
| **P0** | **冶炼/熔炼 Smelting** | 把矿石变金属锭的关键环节 | 金属工坊文档**直接从"金属锭"开始**，跳过了 ore → ingot |
| **P0** | **玻璃 Glass** | 历史上是奢侈品 + 化学副产；Venetian cristallo 是 R 招牌 | 林业文档说 charcoal_hp 给玻璃"+5% yield"——**玻璃业本体不存在** |
| **P0** | **化学/炼金 Chemistry & Alchemy** | 染料、墨水、肥皂、火药、灰碱、酸；魔法的"科学拟身" | 多处引用 lye/potash/绿矾/明矾/硝石/油性墨；**没有专属文档** |
| **P0** | **采盐 Salt** | 烹饪/腌制/皮革都依赖 | 处处用，没人产 |
| **P1** | **陶瓷/瓮罐 Ceramics & Pottery** | 容器、贸易品、地中海生活 | 只在建筑业被提到（赤陶/彩砖/glaze） |
| **P1** | **渔业/水产 Fishing & Aquaculture** | DF 有，咱们没有；地中海/北欧文明高度依赖 | 烹饪用"小鱼/熏鱼"，但没人捕鱼 |
| **P1** | **木工/细木匠 Carpentry & Joinery**（区别于林业） | 家具、容器、机械部件、弓 | 林业只到 board，桶在酿造里只有名字，弓在火器章节明确说"不在这里" |
| **P1** | **桶匠 Cooperage**（拆出） | 一切液体储运基础 | 酿造文档里只有名字 |
| **P1** | **油脂/肥皂/蜡烛 Oil-Soap-Candle** | 副产闭环，DF 标志性 | 处处引用 tallow/lard/wax；**没有专属文档** |
| **P1** | **弓匠 + 弩匠 Bowyer / Crossbow-maker** | 远程武器主线，金属工坊已明确"在木工房" | 没有专属文档 |
| **P2** | **机械工程 Mechanical Engineering**（水轮、风车、齿轮、绞车、起重） | 给所有"动力工坊"提供基底，DF 标志性 | 农业/纺织/造纸/林业都引用了"水力 / 风力"——**没人定义动力系统** |
| **P2** | **水利与卫生 Hydraulics & Sanitation**（aqueduct、排水、浴场、磨坊水道） | 罗马/中世纪城市质感的关键 | 散见于建筑业 Set C3 + 农业 irrigation；**没有统一规范** |
| **P2** | **铸币与货币 Mint & Coinage**（hammered C → milled R 早期） | 经济/贸易/腐败/掺铅事件 | 完全没提 |
| **P2** | **珠宝/宝石加工 Jewelry & Gem-cutting** | DF 标志性，矮人狂热的高潮 | 完全没提 |
| **P3** | **道路/桥梁/驿站 Roads, Bridges, Postways** | 罗马道路 + 中世纪桥 + 文艺复兴邮政 | 完全没提 |
| **P3** | **航海/造船 Shipbuilding** | "Naval Timber"已经在林业打底，但下游不在 | 林业 R 期已经准备好 keel/mast 等材料，但没有造船业 |
| **P3** | **学院/学问 Scholarship & Academy** | 大学（M 期）+ 文艺复兴学院（R 期），与魔法/化学/书籍闭环 | 完全没提 |

### 3.3 已在综合文档但建议拆出独立 SPEC 的内容

- 石材工坊 → 建议拆为独立 **STONEWORKS_SPEC.md**（家具/雕像/铭刻自成体系）
- 金属工坊 → 拆为 **METALWORKS_SPEC.md**
- 火器工坊 → 拆为 **FIREARMS_SPEC.md**
- 攻城武器弹头（已在金属工坊提到要分出来）→ **SIEGE_AMMUNITION_SPEC.md**

---

## 4) 软背景体系（这是当前**最大的空白**）

### 4.1 现状

| 现有 spec/挂钩 | 已经准备好什么 |
|---|---|
| MATERIALS_SPEC `mana_conductivity` | 物质魔法导/抗的整数字段，已经预留 |
| MATERIALS_SPEC `mythic gates magical content/loot tiers` | "神话"等级会门控魔法内容（注释里提到，未展开） |
| CREATURE_SPEC `traits`/`needs`/`values`/`factions` | 性格/需求/价值观/派系系统的占位 + 加载顺序 |
| CREATURE_SPEC §11 Open Questions | 显式列出"Faction culture/ideology binding to values" 和 "Cybernetics/mutations (Qud/CDDA flavor)" |
| /docs/ PDFs | 两份高质量调研（Magic & Occultism、Fantasy Creatures Origins）**没有被转化为 spec** |
| DF 既有元素 | 神器（artifact）、传说模式（legends）、生物部位伤害已经在 GAME_ARCHITECTURE 提到 |

### 4.2 必须补的 6 大软背景文档

下列每一条都建议独立成 SPEC（命名按现有命名规范）：

| 优先级 | 文档名 | 内容大纲 |
|---|---|---|
| **P0** | **WORLD_LORE_SPEC.md** | 世界基调（时代/魔幻浓度/禁止线）、世界历史时间线骨架、文明圈、地理格局；本报告 §1+§2 的精华版本 |
| **P0** | **PANTHEON_RELIGION_SPEC.md** | 神明体系（多神为主 + 几个一神文明）、神职/牧师/教士、祈祷与显圣、节庆、禁忌；DF-style worldgen 神祇 |
| **P0** | **MAGIC_SYSTEM_SPEC.md** | 魔法源（神授/学院/血脉/古代禁忌）、魔法学派（元素/炼金/死灵/预言/附魔/召唤/真名）、魔力代价、施法规则、与 mana_conductivity 的连接 |
| **P0** | **BESTIARY_SPEC.md** | 神话生物分级（凡兽/灵兽/妖物/巨怪/古神），北欧/凯尔特/阿拉伯/克苏鲁四大支线；引用现有 PDF |
| **P1** | **CIVILIZATIONS_FACTIONS_SPEC.md** | 玩家阵营 + 6–10 个邻邦文明的种族/政体/经济/宗教模板；与 incident director + edge-band 接入 |
| **P1** | **CULTURE_VALUES_SPEC.md** | 价值观维度（DF 风格的 axes）、文化习俗 tag、艺术风格 tag、与 creature.values / faction binding 接入 |

### 4.3 关于"中低魔法 + 神话生物 + 神明 + 轻量克苏鲁/北欧/阿拉伯"的具体素材建议

> 以下每条**都已被 `/docs/` 下的两份 PDF 验证有现实神话根源**，可以直接落地。

**A. 魔法学派**（建议保留 4–5 个，避免 D&D 9 学派的过载）

1. **元素魔法 Elemental** — 古希腊四元素 + 帕拉塞尔斯四精灵（gnomes/undines/sylphs/salamanders）。落地：与 fluid/field 系统挂钩。
2. **炼金 Alchemy** — 帕拉塞尔斯/Jabir ibn Hayyan/Hermes Trismegistus 传统。落地：与化学工坊融合，最高产物 = 哲人石 / 长生药 / 高级合金。**这是中低魔法世界里最"自然"的魔法**。
3. **死灵 Necromancy** — DF 已有"夜物 night creature"传统，加上中世纪 grimoire / Witch of Endor / Lich-King。落地：禁忌系统，使用会触发派系敌意/腐化条。
4. **预言/占卜 Divination** — 德尔斐神谕、罗马占卜、占星术、玻璃球凝视。落地：与 storyteller / incident director 挂钩，提供"模糊预警"（reveal upcoming incidents with uncertainty）。
5. **真名/符文 True Names & Runes** — 北欧 runes、《所罗门钥匙》的恶魔印记、埃及伊西斯抢拉的真名。落地：与神器/铭刻系统挂钩，绑定 "name vs control" 玩法。

**B. 神话生物分层**（按"凡—妖—古—神"四级）

| 级别 | 代表生物 | 来源 | 难度/出现频率 |
|---|---|---|---|
| 凡兽 | 普通狼、熊、野猪、毒蛇 | 自然 | 常见 |
| 灵兽 | 独角兽、狮鹫、凤凰、人鱼、半人马 | 希腊/罗马/中世纪寓言 | 区域性，可被驯化/猎杀 |
| 妖物 | 巨魔 troll、哥布林、食人魔、夜行鬼 striga、女妖 banshee、狼人、吸血鬼 | 北欧/凯尔特/斯拉夫 | 边缘地区 / 夜晚 |
| 古怪 | 巨龙（Fafnir 型/Smaug 型）、九头蛇 hydra、海怪 kraken、巨型蛇 leviathan、巨人 jötnar | 北欧/希腊/圣经 | 罕见，史诗级遭遇 |
| 神器/构造 | 哥连 golem（犹太）、homunculus（炼金）、自动机 automaton（希腊—希罗 Heron）、人偶 | 犹太/炼金/希腊 | 玩家可制造 |
| 灵体 | 鬼魂 ghost、亡灵 wight、风灵 sylph、水妖 undine | 普世/帕拉塞尔斯 | 与场域挂钩 |
| 阿拉伯支线 | 马里德 marid（水）、伊夫利特 ifrit（火）、食尸鬼 ghul（沙漠）、灯神 djinn | 1001 夜 / al-Qazwini | 沙漠生物群落 + 神器 |
| 克苏鲁支线 | 深潜者 deep ones（沿海雾港）、星之眷族（陨石坑/地底）、禁忌书籍（读者发疯条） | Lovecraft + 中世纪禁书传说 | 极罕见，触发文明级风险 |

**C. 神明体系**（建议混合 DF worldgen 式生成 + 几个固定神祇骨架）

- **生成式神祇**: 每次 worldgen 产生 N 个神，每个神有：属性集合（火/海/智慧/丰收等）、教派阵营（光/暗/中立）、外貌、神迹关键词。
- **固定原型库**（避免 worldgen 出现毫无文化感的神名）:
  - 锻造神 / 大地母神 / 死亡之神 / 黎明女神 / 海主 / 战神 / 智慧神 / 月女神 / 丰饶神 / 旅行神
  - 每个原型有 3–5 个文明特定的本地命名
- **教派类型**:
  - 多神祠庙（罗马—希腊式 / 北欧式）
  - 单神先知（中东沙漠传统）
  - 万物有灵（凯尔特/森林文化）
  - 祖灵崇拜（高山/草原文化）
  - 禁忌邪教（克苏鲁/暗黑）
- **机制接入**: 神职可施神术（divine cast），祭祀提供士气/buff，亵渎触发诅咒，神器是神明显圣物

**D. 文明骨架**（建议先做 6 个邻邦 + 1 个玩家 + 1 个废墟）

| 文明 | 原型 | 关键标签 | 主要外交角色 |
|---|---|---|---|
| 玩家"人类要塞"分支 | 中世纪欧洲—罗马混血 | 砖石、铁器、多神祠庙 | — |
| 山地矮人 | DF 矮人 + 北欧 dvergar | 地下、锻造、酒、艺品 | 贸易（金属/宝石） |
| 森林精灵 | DF 精灵 + 凯尔特 | 木器、慢科技、自然魔法 | 偶尔贸易 + 经常因伐木冲突 |
| 草原游牧 | 蒙古 / 早期匈人 / 帕提亚 | 马、弓骑、皮草 | 突袭 + 偶尔朝贡 |
| 沙漠苏丹国 | 阿拉伯 / 波斯 | 灯神/星术/玻璃 | 长线奢侈品贸易 |
| 海上城邦 | 威尼斯 / 热那亚 / 腓尼基 | 玻璃/银行/造船 | 主要商队来源 |
| 哥布林游团 | DF 哥布林 + 斯拉夫森林妖 | 袭击、奴隶贸易 | 几乎只袭击 |
| 古王国废墟 | Atlantis / 黎凡特古帝国 + 克苏鲁式禁忌 | 神器、知识、风险 | 探索/挖掘对象，不是外交 |

**E. 价值观/文化轴**（参考 DF + DnD）

DF 已经有的 24 维 facets 太重；建议 8–10 维即可：

`tradition ↔ innovation`, `martial ↔ pacifist`, `materialist ↔ spiritual`, `hierarchical ↔ egalitarian`, `xenophile ↔ xenophobe`, `austere ↔ hedonist`, `lawful ↔ free`, `honour ↔ pragmatic`

每个 axis 是 -100..+100。文明、家族、个人都有 axis 评分。冲突来源于 axis 差距。

---

## 5) 头脑风暴：参考当前火爆中世纪/奇幻/DnD 题材可以"借"的特色机制

> 已经被现有 PDF 验证过的素材标 ✅；纯主观建议标 💡

### 5.1 来自 The Witcher / 巫师 3（斯拉夫民俗）

- ✅ 怪物分门别类，每种有弱点和适配油剂/法印 → 落地：**狩魔系统**，与化学/炼金挂钩
- 💡 **诅咒物品**：作为陷阱/挑战/任务种子
- 💡 **猎魔人式 NPC**：偶尔到访玩家要塞，提供怪物清剿合约

### 5.2 来自 D&D / Baldur's Gate / 龙与地下城

- ✅ 法术学派系统 → 见 §4.3 A
- 💡 **职业 class** 隐性轴（战士/法师/盗贼/牧师/吟游诗人）作为 creature 模板的可选 trait 组
- 💡 **冒险者公会** → 玩家可雇佣外人远征/护送

### 5.3 来自 Warhammer / 黑色奇幻

- ✅ 混乱 / 腐化 系统 → 落地为**禁忌科技与禁忌魔法的"腐化条"**
- 💡 **战团/教派**作为可派遣力量
- 💡 **变异**：长期接触禁忌物质会产生不可逆 trait

### 5.4 来自 Crusader Kings / Total War

- 💡 **家族谱系** + 联姻 + 继承 → 玩家要塞领主线
- 💡 **派系内权力斗争**：贵族 vs 平民 vs 教士 vs 工匠
- 💡 **王朝节点事件**：成年礼、加冕、谋杀、瘟疫

### 5.5 来自 Crusader Kings 3 / Game of Thrones / Wheel of Time

- 💡 **传说与流言**机制：传说作为模因传播，影响 NPC 决策
- 💡 **预言绑定**：玩家要塞被卷入某个 worldgen 期就生成的"预言"

### 5.6 来自 Kingdom Come: Deliverance / Mount & Blade（写实低魔向）

- ✅ 详细身体部位 + 锁链甲/板甲/锁子甲分层 → 已经落地
- 💡 **草药与放血医学**：作为 R 前唯一医学手段，与化学/炼金挂钩
- 💡 **天气—战术耦合**：泥泞、雪、雾影响军队

### 5.7 来自 Dark Souls / Bloodborne / 黑色奇幻

- 💡 **古老火焰/古老月亮**式宏观背景（一句话的世界基调诗）
- 💡 **遗物作为知识载体**：神器自带历史片段
- 💡 **理智 sanity 子系统**：克苏鲁支线触发条件

### 5.8 来自 RimWorld

- ✅ Storyteller / Incident Director → 已经落地 INCIDENT_DIRECTOR_SPEC
- 💡 **关系系统**触发的剧本（罗曼/复仇/失踪）
- 💡 **思绪 thought**系统的"事件→情绪"链

### 5.9 来自 Caves of Qud

- ✅ Sultan 历史 → DF 已经做到，咱们也准备做
- ✅ 派系信誉 → 加入文明 SPEC
- 💡 **古遗物 = 知识碎片** → 印刷/学院系统的下游

### 5.10 来自 Songs of Syx

- 💡 **种族间偏好 LUT**（哪些种族喜欢/讨厌共处）
- 💡 **大规模阅兵和会战**（远期目标）

---

## 6) 建议的新增/拆分文档清单（一次成型）

### 6.1 新增产业链 SPEC（共 15 份）

按优先级 P0 → P3：

1. ⭐ `docs/Mining_Industry_Design.md` (P0) — 露天/坑道/矿脉/工具 C/M/R；衔接 MININGSYSTEM_SPEC
2. ⭐ `docs/Smelting_and_Metallurgy_Design.md` (P0) — ore → ingot → alloy；含铁、铜、青铜、铅、锡、白银、黄金、铅—锡—锑（字模合金）、青铜钟
3. ⭐ `docs/Glassmaking_Industry_Design.md` (P0) — 罗马吹制 C → 中世纪平板/彩窗 M → 威尼斯 cristallo R
4. ⭐ `docs/Chemistry_and_Alchemy_Industry_Design.md` (P0) — 灰碱、苏打、醋酸、绿矾、明矾、硝石、硫磺、火药、油性墨、铁胆墨、染料前体；R 期对接炼金
5. ⭐ `docs/Salt_Industry_Design.md` (P0) — 海盐晒板 / 盐井 / 岩盐 / 煮盐
6. `docs/Ceramics_Pottery_Industry_Design.md` (P1)
7. `docs/Fishing_and_Aquaculture_Design.md` (P1)
8. `docs/Carpentry_and_Joinery_Design.md` (P1) — 含弓匠拆分
9. `docs/Cooperage_Design.md` (P1) — 桶匠
10. `docs/Oil_Soap_Candle_Industry_Design.md` (P1)
11. `docs/Bowyer_Crossbow_Design.md` (P1)
12. `docs/Mechanical_Engineering_Design.md` (P2) — 动力系统标准
13. `docs/Hydraulics_and_Sanitation_Design.md` (P2)
14. `docs/Mint_and_Coinage_Design.md` (P2)
15. `docs/Jewelry_Gemcutting_Design.md` (P2)

### 6.2 拆分现有综合文档

- `docs/STONEWORKS_Design.md` ← 从 PROCESS_CHAIN 抽出
- `docs/METALWORKS_Design.md` ← 从 PROCESS_CHAIN 抽出
- `docs/FIREARMS_Design.md` ← 从 PROCESS_CHAIN 抽出
- `docs/SIEGE_AMMUNITION_Design.md` ← 攻城弹药统一

### 6.3 全新软背景 SPEC（共 6 份；进入根目录，与现有 *_SPEC.md 体例统一）

- `WORLD_LORE_SPEC.md`
- `PANTHEON_RELIGION_SPEC.md`
- `MAGIC_SYSTEM_SPEC.md`
- `BESTIARY_SPEC.md`
- `CIVILIZATIONS_FACTIONS_SPEC.md`
- `CULTURE_VALUES_SPEC.md`

### 6.4 衍生 SPEC（中长期）

- `LANGUAGES_AND_SCRIPTS_SPEC.md` (P3) — 语言学/碑铭/外交
- `CALENDAR_FESTIVALS_SPEC.md` (P3) — 季节性事件
- `GOVERNMENT_LAW_SPEC.md` (P3) — 接续 DF 司法体系
- `SCHOLARSHIP_ACADEMY_SPEC.md` (P3) — 学院/图书馆/学者

---

## 7) 建议的执行顺序（给 Codex / Claude Code 的工单优先级）

**第 1 波（先做软背景基线，因为它会约束后续产业链的命名/物种/材料）**

1. WORLD_LORE_SPEC（最先；其他都引用它）
2. PANTHEON_RELIGION_SPEC + BESTIARY_SPEC（并行）
3. MAGIC_SYSTEM_SPEC（依赖前两者）

**第 2 波（产业链补全 P0）**

4. Salt + Mining + Smelting + Glass + Chemistry/Alchemy（并行；它们彼此依赖度低，且都是其他链的源头）

**第 3 波（社会层）**

5. CIVILIZATIONS_FACTIONS_SPEC + CULTURE_VALUES_SPEC（并行）

**第 4 波（产业链 P1 + 文档拆分）**

6. Ceramics, Fishing, Carpentry, Cooperage, Oil-Soap-Candle, Bowyer（并行）
7. 拆分 PROCESS_CHAIN → STONEWORKS / METALWORKS / FIREARMS / SIEGE_AMMUNITION

**第 5 波（基础设施 P2）**

8. Mechanical_Engineering, Hydraulics, Mint, Jewelry（并行）

**第 6 波（长尾 P3）**

9. 道路/航海/语言/历法/政府/学院

---

## 8) 风险与开放问题

1. **越界冲突**: 农业声明的"不引入哥伦布作物"vs 纺织 R 期的 cochineal → 必须 §2.1 决策
2. **魔法浓度漂移**: 一旦开始写 MAGIC_SYSTEM_SPEC，很容易越界到 D&D 高魔。需要在 WORLD_LORE_SPEC 一锤定音"魔法稀有 + 代价沉重"的硬约束
3. **DF 神器系统兼容**: 我们准备保留 DF 的 fey-mood + artifact 系统吗？这会大幅影响神话生物 + 神迹的接入点
4. **克苏鲁支线尺度**: 是"全图随机散落几个禁书"还是"一个完整的远古文明遗迹"？前者轻量，后者会成为长期内容线
5. **种族数量**: 是只有"人类要塞"一个玩家种族（保持"Human Fortress"标题），还是开放矮人/精灵 embark？这影响 CIVILIZATIONS_FACTIONS_SPEC 的写法
6. **道德对齐**: DF 是道德无关的纯模拟，咱们要不要保留？还是引入"罪/荣誉/腐化"轴线？

---

## 9) 与 DF 的差异定位（一句话版本）

| 维度 | DF 现状 | 本作目标（基于本报告） |
|---|---|---|
| 时代跨度 | 古典—中世纪—部分 R | 古典—文艺复兴**早期**（硬封顶） |
| 魔法浓度 | 中高（生成式 magic system，可怕） | **中低**（魔法稀有 + 神话点缀 + 神明真实但少现身） |
| 神话支线 | 北欧—DF 原创混合 | **北欧 + 凯尔特 + 阿拉伯 + 轻量克苏鲁 + 经典希腊罗马** |
| 产业链 | 极广但浅 | **较广 + 中等深度 + 时代台阶清晰** |
| 文明系统 | worldgen + history | **保留 + 增加 axis values + 文化原型库** |
| 玩家种族 | 矮人为主 | **人类为主**（标题已锁定 Human Fortress） |

---

## 附录 A — 用户问题 #1 的直接回答

> "我们的游戏软背景设计 尤其是我们的文档是否符合古典/中世纪/文艺复兴时期科技树？还有什么内容可以添加？"

**直接回答**:

1. **是否符合**: 整体高度符合。8 条主流产业链 + 综合工坊文档**没有一条需要重写**，仅 3–4 处微调（cochineal、Stationers' Company、Maximilian、Mortar/Musket）。
2. **真正需要补的不是"修"，是"添"**:
   - **产业链宽度**: 至少 15 条核心产业链缺失（最关键 5 条 = 采矿/冶炼/玻璃/化学/盐）
   - **软背景**: 几乎完全空白；最关键 6 份 = WORLD_LORE / PANTHEON / MAGIC_SYSTEM / BESTIARY / CIVILIZATIONS / CULTURE_VALUES
3. **建议立刻起草的下一份文档**: `WORLD_LORE_SPEC.md`，因为它会**锁死**所有后续文档的时代/魔幻/文化基调，避免后续 Codex 写出来的文档自相矛盾。

---

（完）
