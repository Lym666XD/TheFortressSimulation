# Leatherwork — Simplified, DF-Aligned (Classical / Medieval / Renaissance)

> **对应工坊**: [Tannery](../workshops/Tannery.md)
> 工坊文档是 markdown 实现层 + JSON 对齐；本 industry md 提供历史锚点与设计原理。


**Version:** 1.0 · **Status:** Ready for implementation  
**Design intent:** Keep Dwarf Fortress (DF) flow (`Butcher → Tannery → Leather`) intact, then add a few era-gated options that attach light **quality tags** (no heavy chemistry, no city-policy).  
**Quality tags used:** `heavy` (sole/harness), `white` (gloves/bindings), `suede` (soft), `quality+1` (refined), `prestige` (trade luxury; Morocco/Cordovan).

---

## 1) Goals (DF parity)
- **Fast core loop stays:** `raw_hide → leather_common` at the **Tannery**.  
- **Era steps = small perks:** unlock optional recipes with mild bonuses (durability/price/appearance).  
- **Simple buildings:** one **Tannery** for almost everything; add **Currier’s Bench** (M/R) and optional **Dye Bench** (R).  
- **Light hazards:** a simple `stench` tag only.

---

## 2) Era → Process Chain → IO → Unlocks

### Classical (C)
**A. Quick Tanning (DF baseline)**  
- Chain: raw hide → Tannery → leather  
- IO: `raw_hide ×1 → leather_common ×1` (short)  
- Unlocks: **Tannery (C)**

**B. Bark Tanning (optional, harder leather)**  
- Chain: raw hide + tannin bark → long soak → heavy leather  
- IO: `raw_hide ×1 + tannin_bark ×1 → leather_heavy ×1` (long)  
- Unlocks: **Tannery (C)**  
- Use: soles, straps; give **Durability +1** *or* **Price +10%**.

**C. Alum-Tawed “White” (light skins)**  
- Chain: light skin + alum + salt → white leather  
- IO: `raw_skin_light ×1 + alum ×1 + salt ×1 → leather_white ×1` (medium)  
- Unlocks: **Tannery (C)**  
- Use: fine gloves, bookbindings; **Appearance +1** *or* **Price +10%**.

---

### Medieval (M)
**A. Batch Tanning (throughput boost)**  
- IO: `raw_hide ×3 → leather_common ×3` (medium, **×1.25** throughput)  
- Unlocks: **Tannery (M: batch mode)**

**B. Oiled Finishing (curried)**  
- IO: `leather_* ×1 + tallow_or_wax ×1 → leather_*_finished ×1` (short)  
- Unlocks: **Currier’s Bench (M)**  
- Effect: **Durability +1** *or* **Price +10%** (pick one globally).

**C. Suede (simplified oil-tan)**  
- IO: `raw_skin_light ×1 + fish_oil ×1 → leather_suede ×1` (medium)  
- Unlocks: **Tannery (M)**  
- Use: soft gloves/cloths; **Comfort/Appearance +1** tag.

---

### Renaissance (R)
**A. Refinement (quality+1)**  
- IO: `leather_* ×1 → leather_*(quality+1) ×1` (medium)  
- Unlocks: **Currier’s Bench (R mode)**

**B. Prestige Labels (trade rarity)**  
- Morocco (sumac goat): `raw_skin_goat ×1 + sumac_tannin ×1 (+ dye ×1) → leather_morocco ×1` (medium)  
- Cordovan (horse shell): `raw_horse_shell ×1 + tallow_or_wax ×1 → leather_cordovan ×1` (long)  
- Unlocks: **Tannery (R)** (+ optional **Dye Bench (R)**)  
- Effect: **Price +25%**, prestige tag; combat stats unchanged.

---

## 3) Items (minimal set)
- **Raw:** `raw_hide`, `raw_skin_light` (sheep/goat/calf), `raw_horse_shell` (rare)  
- **Leathers:** `leather_common`, `leather_heavy`, `leather_white`, `leather_suede`, `leather_morocco`, `leather_cordovan`  
- **Additives:** `tannin_bark`, `alum`, `salt`, `tallow_or_wax`, `fish_oil`, `sumac_tannin`, `dye_basic`  
- **Scraps (optional):** `hair_scrap`, `leather_scrap`

---

## 4) Recipes (small, era-gated)

**C**  
1) Quick Tan — `raw_hide ×1 → leather_common ×1` @Tannery (short)  
2) Bark Tan (Heavy) — `raw_hide ×1 + tannin_bark ×1 → leather_heavy ×1` @Tannery (long)  
3) Alum White — `raw_skin_light ×1 + alum ×1 + salt ×1 → leather_white ×1` @Tannery (medium)

**M**  
4) Batch Tan — `raw_hide ×3 → leather_common ×3` @Tannery (medium, ×1.25 throughput)  
5) Oiled Finish — `leather_* ×1 + tallow_or_wax ×1 → leather_*_finished ×1` @Currier’s (short)  
6) Suede — `raw_skin_light ×1 + fish_oil ×1 → leather_suede ×1` @Tannery (medium)

**R**  
7) Refine — `leather_* ×1 → leather_*(quality+1) ×1` @Currier’s (medium)  
8) Morocco — `raw_skin_goat ×1 + sumac_tannin ×1 (+ dye_basic ×1) → leather_morocco ×1` @Tannery (medium)  
9) Cordovan — `raw_horse_shell ×1 + tallow_or_wax ×1 → leather_cordovan ×1` @Tannery (long)

---

## 5) Workshops
- **Tannery (C/M/R):** all tanning; batch mode (M), prestige (R).  
- **Currier’s Bench (M/R):** oiled finish (M), refinement (R).  
- **Dye Bench (R, optional):** color/appearance only.

---

## 6) Balance defaults
- Durations: Quick (short); White/Suede (medium); Heavy/Cordovan (long); Refinement (medium).  
- Throughput: Batch tan **×1.25**.  
- Bonuses (pick one per group, no stacking on combat):  
  - `heavy` → Durability +1 **or** Price +10%  
  - `white` → Appearance +1 **or** Price +10%  
  - `suede` → Comfort/Appearance +1  
  - `*_finished` **or** `quality+1` → Durability +1 **or** Price +10%  
  - `prestige` (Morocco/Cordovan) → Price +25%  
- Hazard tag: `stench` only (mood/room requirement if desired).

---

## 7) DF integration
- Keep DF’s **Tannery** job for 1/2/3/6/8/9;  
- **Currier’s Bench** handles 5/7;  
- Tallow → Soap chain unchanged (just used as finishing additive).

---

---

# 皮革业 — 简化版、对齐《矮人要塞》（古典 / 中世纪 / 文艺复兴）

**版本：**1.0　**状态：**可直接实现  
**设计意图：**保留 DF 的核心闭环（`屠宰 → 制革作坊 → 皮革`），时代推进只解锁少量**质量标签**的配方（不引入复杂化学/政策）。  
**质量标签：**`heavy`（重皮）、`white`（白革）、`suede`（麂皮）、`quality+1`（精整）、`prestige`（精品，摩洛哥/科尔多瓦）。

---

## 1）目标（贴合 DF）
- **主线不变：**`raw_hide → leather_common` 在**制革作坊**完成。  
- **时代小台阶：**少量可选配方带来温和加成（耐久/售价/外观）。  
- **建筑简单：**一座**制革作坊**搞定大多数流程；新增**整饰台**（中/后期）和**（可选）染色台**（后期）。  
- **轻危害：**仅有 `stench`（臭气）标签。

---

## 2）时代 → 工艺链 → 投入/产出 → 解锁

### 古典（C）
**A. 快速制革（DF 主线）**  
- 链路：生皮 → 制革作坊 → 皮革  
- IO：`raw_hide ×1 → leather_common ×1`（短）  
- 解锁：**制革作坊（C）**

**B. 树皮鞣（可选，较硬）**  
- 链路：生皮 + 树皮粉 → 长时浸渍 → 重皮  
- IO：`raw_hide ×1 + tannin_bark ×1 → leather_heavy ×1`（长）  
- 解锁：**制革作坊（C）**  
- 用途：鞋底/带具；给 **耐久+1** 或 **售价+10%**（二选一）。

**C. 明矾白革（轻皮）**  
- 链路：轻皮 + 明矾 + 盐 → 白革  
- IO：`raw_skin_light ×1 + alum ×1 + salt ×1 → leather_white ×1`（中）  
- 解锁：**制革作坊（C）**  
- 用途：细工手套/装帧；给 **美观+1** 或 **售价+10%**。

---

### 中世纪（M）
**A. 批量制革（吞吐↑）**  
- IO：`raw_hide ×3 → leather_common ×3`（中，**×1.25** 吞吐）  
- 解锁：**制革作坊（M：批量模式）**

**B. 油整饰（curried）**  
- IO：`leather_* ×1 + tallow_or_wax ×1 → leather_*_finished ×1`（短）  
- 解锁：**整饰台（M）**  
- 效果：**耐久+1** 或 **售价+10%**（全局择一，不叠）。

**C. 麂皮（油鞣简化）**  
- IO：`raw_skin_light ×1 + fish_oil ×1 → leather_suede ×1`（中）  
- 解锁：**制革作坊（M）**  
- 用途：柔软手套/擦拭布；**舒适/美观+1** 标签。

---

### 文艺复兴（R）
**A. 精整（quality+1）**  
- IO：`leather_* ×1 → leather_*(quality+1) ×1`（中）  
- 解锁：**整饰台（R 模式）**

**B. 精品标签（贸易稀有）**  
- 摩洛哥（sumac 羊皮）：`raw_skin_goat ×1 + sumac_tannin ×1（+ dye ×1 可选） → leather_morocco ×1`（中）  
- 科尔多瓦（马臀壳层）：`raw_horse_shell ×1 + tallow_or_wax ×1 → leather_cordovan ×1`（长）  
- 解锁：**制革作坊（R）**（染色可用**染色台（R，可选）**）  
- 效果：**售价+25%**，仅 Prestige，不改战斗。

---

## 3）物品（精简）
- **原皮：**`raw_hide`、`raw_skin_light`（羊/山羊/小牛）、`raw_horse_shell`（稀有）  
- **皮革：**`leather_common`、`leather_heavy`、`leather_white`、`leather_suede`、`leather_morocco`、`leather_cordovan`  
- **辅料：**`tannin_bark`、`alum`、`salt`、`tallow_or_wax`、`fish_oil`、`sumac_tannin`、`dye_basic`  
- **碎料（可选）：**`hair_scrap`、`leather_scrap`

---

## 4）配方（小集合，按时代）
**C**  
1）快速制革——`raw_hide ×1 → leather_common ×1`（短）@制革作坊  
2）树皮重皮——`raw_hide ×1 + tannin_bark ×1 → leather_heavy ×1`（长）@制革作坊  
3）白革（明矾）——`raw_skin_light ×1 + alum ×1 + salt ×1 → leather_white ×1`（中）@制革作坊

**M**  
4）批量制革——`raw_hide ×3 → leather_common ×3`（中，×1.25）@制革作坊  
5）油整饰——`leather_* ×1 + tallow_or_wax ×1 → leather_*_finished ×1`（短）@整饰台  
6）麂皮——`raw_skin_light ×1 + fish_oil ×1 → leather_suede ×1`（中）@制革作坊

**R**  
7）精整——`leather_* ×1 → leather_*(quality+1) ×1`（中）@整饰台  
8）摩洛哥——`raw_skin_goat ×1 + sumac_tannin ×1（+ dye_basic ×1） → leather_morocco ×1`（中）@制革作坊  
9）科尔多瓦——`raw_horse_shell ×1 + tallow_or_wax ×1 → leather_cordovan ×1`（长）@制革作坊

---

## 5）建筑
- **制革作坊（C/M/R）：**全流程；M 有“批量模式”，R 可做精品标签。  
- **整饰台（M/R）：**M 做油整饰；R 做精整（quality+1）。  
- **染色台（R，可选）：**只加颜色/美观。

---

## 6）默认平衡
- 时长：快速=短；白革/麂皮=中；重皮/科尔多瓦=长；精整=中。  
- 吞吐：中世纪“批量制革” **×1.25**。  
- 加成（每组二选一、且不叠战斗属性）：  
  - `heavy` → **耐久+1** 或 **售价+10%**  
  - `white` → **美观+1** 或 **售价+10%**  
  - `suede` → **舒适/美观+1**  
  - `*_finished` / `quality+1` → **耐久+1** 或 **售价+10%**  
  - `prestige`（摩洛哥/科尔多瓦）→ **售价+25%**  
- 危害：仅 `stench`（心情/通风需求，按需启用）。

---

## 7）与 DF 的挂接
- **Tannery**：承接 1/2/3/6/8/9 号配方；  
- **Currier’s Bench**：承接 5/7；  
- **脂肪→肥皂**链不变，`tallow_or_wax` 只作整饰耗材。