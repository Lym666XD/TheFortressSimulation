# Brewing Industry — Design Doc (Classical / Medieval / Renaissance) · Bilingual (EN/ZH)

> **对应工坊**: [Agri_Brew_Works](../workshops/Agri_Brew_Works.md)
> 工坊文档是 markdown 实现层 + JSON 对齐；本 industry md 提供历史锚点与设计原理。


**Version:** 1.0 · **Status:** Ready for implementation  
**Scope:** Malt beverages (ale/beer), mead, wine, cider/perry, vinegar, brandy (distillation).  
**Assumptions:** No durability system; bonuses via speed, logistics, shelf life, morale. Hooks into your **Agriculture** (grains, grapes, apples, herbs, hops) and **Husbandry** (honey/bee, spent grain → feed).  
**DF contrast:** Dwarf Fortress uses a single *Still* step for all booze; here we split into **malting → mash/boil → ferment → package**, adding optional era perks and closed loops (spent grain, yeast reuse).

---

## 1) Goals
- Keep loops **lightweight** and **era-flavored** (Classical = unhopped & wine/mead; Medieval = hops & monastic; Renaissance = purity law, distillation, vinegar).  
- Provide **clear IO** per chain; standardize containers (**barrel / rock pot / wooden cask**).  
- Add **logistics advantages** (stack sizes, batch multipliers) and **shelf-life** differences (hops extends keeping quality).

---

## 2) Era → Process Chain → IO → Unlocks

### Classical (C)
**C1. Wine**  
- Chain: harvest → crush/press → maceration/ferment → rack → cask/jar.  
- IO: **grape ×10 → must ×8 → wine ×6 + lees ×2** (lees → vinegar starter/compost).  
- Unlocks: *Wine Press, Fermentation Vat, Cellar (C)*.

**C2. Unhopped Ale (gruit-style)**  
- Chain: **malting → crush → mash/lauter → (optional gruit herbs) boil → cool → open fermentation**.  
- IO: **barley/wheat ×10 → malt ×8 → ale ×6 + spent_grain ×2** (spent_grain → feed).  
- Unlocks: *Malting Floor/Kiln, Mash Kettle (C)*.

**C3. Mead**  
- Chain: dilute honey → ferment → clarify → package.  
- IO: **honey ×5 + water ×5 → mead ×6 + beeswax residue ×0.2** (wax → candles).  
- Unlocks: *Apiary (from Husbandry), Mead Bench (C)*.

### Medieval (M)
**M1. Monastic Beer (with hops)**  
- Chain: malt → mash/lauter → **boil with hops** → cool → open ferment → cask.  
- IO: **malt ×10 + hops ×1 → beer ×8 + spent_grain ×2**.  
- Unlocks: *Hop Garden (M), Cooperage (M), Coolship/Cooler (M)*.  
- Effect: **Hopped beer has longer shelf life** (logistics tag).

**M2. Cider / Perry**  
- Chain: crush → press → clarify → ferment → cask.  
- IO: **apple/pear ×10 → juice ×8 → cider/perry ×6 + pomace ×2** (pomace → compost/feed).  
- Unlocks: *Cider Press (M)*.

**M3. Dual-Track Option**  
- Keep **unhopped ale** (fast, low materials, short shelf) vs **hopped beer** (longer keeping, safer for trade).

### Renaissance (R)
**R1. Purity-Law Beer (Reinheitsgebot-like label)**  
- Chain: selected malt → mash/step mash → hops boil → cool → ferment/lager → cask.  
- IO: **quality_malt ×10 + hops ×1 → beer_purity ×8 + spent_grain ×2**.  
- Unlocks: *Improved Malt Kiln, Cold Cellar (R), “Purity” policy label* (price/quality tag).

**R2. Distillation (Brandy)**  
- Chain: **wine/low-ABV → alembic still → spirit → (optional aging)**.  
- IO: **wine ×8 → brandy ×3 + vinasse ×1** (vinasse → vinegar/compost).  
- Unlocks: *Alembic Still & Condenser (R)*.

**R3. Vinegar Works**  
- Chain: low-ABV beverage → **acetification** → clarify → package.  
- IO: **low_abv_liquid ×8 → vinegar ×6** (kitchen, pickling, cleaning).  
- Unlocks: *Vinegar Vat & Mother (R)*.

---

## 3) Common Items (minimal set)
**Crops & adjuncts:** barley, wheat/rye/oats, grape, apple/pear, **hops**, **gruit_herbs**, **honey**, **water**  
**Intermediates:** malt, wort, hot_wort, must, juice, **spent_grain**, **yeast_cake**, lees, pomace  
**Finished:** ale (unhopped), **beer (hopped)**, **beer_purity**, mead, wine, cider/perry, brandy, vinegar  
**Containers:** barrel / rock_pot, wooden_cask, bung/pitch, cloth/filters

**Stacking & logistics (suggestion):**  
- malt/wort/booze by **barrel (×8 units)**; **spent_grain by sack (×10)**; hops/gruit by small bundles (×10).  
- Effect: fewer hauling jobs → faster overall throughput.

---

## 4) Workshops
- **Malting Floor / Malt Kiln (C→R)** — germination & kilning  
- **Brewhouse** — mash, lauter, boil  
- **Coolship / Cooler (M→R)** — rapid cooling  
- **Fermentation Vat / Cellar (C→R)** — ferment & mature  
- **Wine Press / Cider Press (C→R)** — pressing  
- **Cooperage (M→R)** — make/repair casks  
- **Apiary (C→R)** — honey/wax supply  
- **Alembic Still (R)** — distillation  
- **Vinegar Works (R)** — acetification

---

## 5) Representative Recipes (balanced defaults)
- **malt_barley:** barley ×10 → malt ×8 @malting_floor (time↑; returns husk)  
- **mash_and_lauter:** malt ×8 + water → wort ×8 + **spent_grain ×2** @brewhouse  
- **boil_hopped:** wort ×8 + hops ×1 → hot_wort ×8 @brewhouse  
- **boil_gruit:** wort ×8 + gruit_herbs ×1 → hot_wort ×8 @brewhouse  
- **ferment_beer:** hot_wort ×8 → beer ×8 + **yeast_cake ×0.2** @fermentation_vat  
- **wine_line:** grape ×10 → must ×8 @wine_press → ferment → wine ×6 + lees ×2  
- **cider_line:** apple ×10 → juice ×8 @cider_press → ferment → cider ×6 + pomace ×2  
- **distill_brandy:** wine ×8 → brandy ×3 + vinasse ×1 @alembic  
- **make_vinegar:** low_abv_liquid ×8 → vinegar ×6 @vinegar_works

---

## 6) Balance & Tags (no durability system)
- **Shelf life:** beer(hopped) / beer_purity keep **longer**; unhopped ale **shorter**.  
- **Morale/feast:** mead/wine add **feast/religious** flavor bonus; beer supports **garrison & expedition** due to keeping.  
- **Industrial loops:** spent_grain → **feed_mix** (Husbandry); yeast_cake → **baking/next pitch**; pomace/lees/vinasse → **compost/vinegar**.  
- **Logistics bonuses:** cask packaging increases hauling efficiency; cold cellar reduces spoilage.  
- **Era perks:** Monastic (M) → stability + small quality tag; Purity (R) → **price/quality label** (no combat stats).

---

## 7) Tech Unlocks (copy to tree)
- **Classical:** Malting Floor, Brewhouse (basic), Wine Press, Mead, Unhopped Ale  
- **Medieval:** Hop Garden, Cooperage, Open Fermenters/Coolship, Cider Press, Monastic Recipes  
- **Renaissance:** Purity Label, Cold Cellar, Alembic Still, Vinegar Works, Decorative Tavernware

---

## 8) DF Integration Notes
- Replace single-step *Still* with **Malting → Brewhouse → Ferment → Package** (four jobs).  
- Keep **rock pot** as low-wood container option.  
- Return **seeds** where applicable (DF parity) and **spent_grain** to Husbandry.

---

---

# 酿造业 — 设计文档（古典 / 中世纪 / 文艺复兴）

**版本：**1.0　**状态：**可直接实现  
**范围：**麦芽酒（艾尔/啤酒）、蜂蜜酒、葡萄酒、苹果酒/梨酒、食醋、白兰地（蒸馏）。  
**前提：**不做耐久；通过**建造/酿造速度、搬运效率、保质期、士气**体现差异。与**农业**（谷物/葡萄/苹果/酒花/香草）与**畜牧业**（蜂蜜/酒糟饲料）闭环。  
**与 DF 的差异：**DF 的 *Still* 一步到位；本设计拆分为**制麦芽→糖化/煮沸→发酵→装桶**，并增加时代加成与副产闭环。

---

## 1）目标
- **轻量且有时代感**：C=无酒花与酒/蜜酒；M=酒花与修道院；R=纯净标签、蒸馏与醋。  
- **清晰 IO** 与标准容器（桶/陶罐/木桶）。  
- **物流优势**：更大堆叠、更少搬运；酒花带来**保质**差异。

---

## 2）时代 → 工艺链 → 投入/产出 → 解锁

### 古典（C）
**C1. 葡萄酒**  
- 链：采收→破皮/压榨→浸渍发酵→换桶→装坛/装桶。  
- IO：**葡萄×10 → 葡萄汁×8 → 葡萄酒×6 + 酒脚×2**（酒脚→醋母/堆肥）。  
- 解锁：**葡萄压榨架、发酵缸、地窖（C）**。

**C2. 无酒花麦酿（gruit 型）**  
- 链：**制麦芽→粉碎→糖化/过滤→（可加草本 gruit）煮沸→冷却→开放发酵**。  
- IO：**大麦/小麦×10 → 麦芽×8 → 麦酒×6 + 酒糟×2**（→ 畜牧饲料）。  
- 解锁：**麦芽楼/麦芽窑、糖化锅（C）**。

**C3. 蜂蜜酒**  
- 链：蜂蜜溶解→发酵→澄清→装坛。  
- IO：**蜂蜜×5 + 水×5 → 蜂蜜酒×6 + 蜂蜡渣×0.2**（→ 蜡烛）。  
- 解锁：**蜂箱（畜牧业）、蜜酒台（C）**。

### 中世纪（M）
**M1. 修道院麦酒（投酒花）**  
- 链：麦芽→糖化/过滤→**酒花煮沸**→冷却→开放发酵→木桶熟成。  
- IO：**麦芽×10 + 酒花×1 → 啤酒×8 + 酒糟×2**。  
- 解锁：**酒花园（M）、木桶匠（M）、冷却槽/冷却器（M）**。  
- 效果：**投酒花 = 保质期更长**（物流标签）。

**M2. 苹果酒/梨酒**  
- 链：粉碎→压榨→澄清→发酵→装桶。  
- IO：**苹果/梨×10 → 果汁×8 → 苹果酒/梨酒×6 + 果渣×2**（→ 堆肥/饲料）。  
- 解锁：**果榨（M）**。

**M3. 双轨说明**  
- **无酒花艾尔**（工期短、材料少、但保质短） vs **投酒花啤酒**（保质长、适合外销）。

### 文艺复兴（R）
**R1. “纯净法令”麦酒（标签）**  
- 链：精选麦芽→糖化/分步煎煮→酒花煮沸→冷却→发酵/窖藏→装桶。  
- IO：**优质麦芽×10 + 酒花×1 → 啤酒（纯净）×8 + 酒糟×2**。  
- 解锁：**改良麦芽窑、冷窖（R）、“纯净”标签/加价**。

**R2. 蒸馏（白兰地）**  
- 链：**葡萄酒/低度酒→蒸馏→酒心→（可陈年）**。  
- IO：**葡萄酒×8 → 白兰地×3 + 酸酵液×1**（→ 醋/堆肥）。  
- 解锁：**蒸馏器/冷凝管（R）**。

**R3. 醋坊**  
- 链：低度酒→**醋化**→澄清→装坛。  
- IO：**低度酒×8 → 食醋×6**（厨用/腌渍/清洁）。  
- 解锁：**醋化罐/醋母（R）**。

---

## 3）物品（最小集合）
**作物与辅料：**大麦、小麦/黑麦/燕麦、葡萄、苹果/梨、**酒花**、**草本（gruit）**、**蜂蜜**、水  
**中间体：**麦芽、麦汁、热麦汁、葡萄汁、果汁、**酒糟**、**酵母渣（酵母饼）**、酒脚、果渣  
**成品：**艾尔（无酒花）、**啤酒（投酒花）**、**纯净标签啤酒**、蜂蜜酒、葡萄酒、苹果酒/梨酒、白兰地、食醋  
**容器：**桶/陶罐、木桶、封缝料、布滤材

**堆叠/物流建议：**  
- 酒类/麦汁以**桶（×8 单位）**为计量；**酒糟以麻袋（×10）**；酒花/草本以小捆（×10）。

---

## 4）工坊
- **麦芽楼 / 麦芽窑（C→R）**  
- **酿造屋（糖化/煮沸）（C→R）**  
- **冷却槽/冷却器（M→R）**  
- **发酵缸 / 地窖（C→R）**  
- **葡萄压榨 / 果榨（C→R）**  
- **木桶匠（M→R）**  
- **蒸馏器（R）**  
- **醋坊（R）**

---

## 5）代表配方（默认倍率）
- **制麦芽：**谷物×10 → 麦芽×8（@麦芽楼，需时）  
- **糖化/过滤：**麦芽×8 + 水 → 麦汁×8 + **酒糟×2**（@酿造屋）  
- **煮沸（投料）：**麦汁×8 +（酒花×1 | 草本×1）→ 热麦汁×8（@酿造屋）  
- **发酵：**热麦汁×8 → 啤酒/艾尔×8 + **酵母渣×0.2**（@发酵缸）  
- **葡萄线：**葡萄×10 → 葡萄汁×8（@压榨）→ 发酵 → 葡萄酒×6 + 酒脚×2  
- **苹果线：**苹果×10 → 果汁×8（@果榨）→ 发酵 → 苹果酒×6 + 果渣×2  
- **蒸馏：**葡萄酒×8 → 白兰地×3 + 酸酵液×1（@蒸馏器）  
- **醋化：**低度酒×8 → 食醋×6（@醋坊）

---

## 6）平衡与标签（不做耐久）
- **保质期：**投酒花啤酒/纯净标签啤酒 **更耐放**；无酒花艾尔 **更易变质**。  
- **士气/筵宴：**蜂蜜酒/葡萄酒在节庆/宗教事件 **加成更高**；啤酒更适合**驻军与远征补给**。  
- **工业闭环：**酒糟→**饲料**；酵母渣→**烘焙/下一批投酵**；果渣/酒脚/酸酵液→**堆肥/食醋**。  
- **物流：**装桶提高搬运效率；冷窖减少腐败。  
- **时代标签：**修道院（M）→稳定与小幅品质；纯净（R）→**售价/品质标签**（不影响战斗）。

---

## 7）解锁总览
- **古典：**麦芽楼、基础酿造屋、葡萄压榨、蜂蜜酒、无酒花艾尔  
- **中世纪：**酒花园、木桶匠、开放式发酵/冷却槽、果榨、修道院配方  
- **文艺复兴：**纯净标签、冷窖、蒸馏器、醋坊、酒馆装饰器具

---

## 8）与 DF 的对接
- 用 **四段工序**替代单一步骤：制麦芽 → 酿造屋（糖化/煮沸）→ 发酵 → 装桶/装坛。  
- 保留 **岩石罐（rock pot）** 作为低木材容器。  
- 继续返还种子（兼容 DF 体验），并把 **酒糟**输出到畜牧业。