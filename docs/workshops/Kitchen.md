# Kitchen 厨房工坊

**对应 JSON**: `data/core/workshops/core_workshop_kitchen.json`（12 attachments）
**对应 industry md**: [Cooking_System_Design.md](../industries/Cooking_System_Design.md)
**era**: C → R
**主要 tags**: workshop, food, kitchen, cooking

---

## 1) 用途与定位

把"灶锅炖煮 / 烤炉烘焙 / 腌熏保藏 / 调味调脂 / R 期香料糖"整合到一座工坊。
- 替代 DF 的单一 Kitchen，但仍保持"少表项 + 好落库"。

**与 Butchery / Agri_Brew_Works 边界**:
- Butchery: 鲜肉 / tallow / lard / bone
- Agri_Brew_Works: 面粉 / 油 / 酒
- Kitchen: 把这些原料做成"成品菜 / 面包 / 馅饼 / 腌肉 / 熏鱼 / 调味品"

---

## 2) Attachment Slots（含 C/M/R 升级链）— 与 JSON 对齐

| slot | 用途 | C | M | R |
|---|---|---|---|---|
| `hearth_cauldron` | 灶锅（炖/煮）| Hearth & Cauldron | Improved Hearth | Stone Hearth & Large Cauldron |
| `oven` | 烘焙 | Hearth Oven | Communal Oven (批量) | Bake Oven (馅饼) |
| `ferment_jar` | 发酵罐 (鱼酱)| Fermentation Jar | (升级) | (升级) |
| `saltery` | 腌制 | — | Salting Bench | Brine Tub & Curing Rack |
| `smokehouse` | 熏制 | — | Smokehouse | Improved Smokehouse |
| `spice_sugar_bench` | 香料糖台 | — | — | Spice & Sugar Bench |
| `dairy_bench` | 乳制（与 Pasture 协作）| Curd Pan | Hard Cheese Press | Cheese Aging Cellar |

---

## 3) 配方索引（按 era）

### 3.1 古典（C）

| 配方 | 输入 | 输出 |
|---|---|---|
| 一锅炖 pottage | grain ×4 + veg/bean ×2 + water ×1 | hot_stew ×8（热食/饱足心情+1）|
| 扁面包 | flour ×2 + water ×1 + salt ×0.1 | flatbread ×4 |
| 鱼酱 garum | small_fish/offal ×4 + salt_coarse ×2 | fish_sauce ×1（调味，使炖/烤成品风味+1）@ferment_jar |
| 鲜奶酪 | raw_milk ×1 | fresh_cheese ×0.6 + whey ×0.4 @dairy_bench |
| 鲜肉烹饪 | meat ×N + 油 / 香草 | prepared_meal_simple ×N |

### 3.2 中世纪（M）

| 配方 | 输入 | 输出 |
|---|---|---|
| 公共面包炉（批量）| dough ×6 + 柴薪 ×1 | bread ×8（批量+） @oven M |
| 浓炖（加脂）| grain ×4 + veg ×2 + fat ×0.5 + water ×1 | thick_stew ×8（风味+1）|
| 腌肉 | meat ×4 + salt ×1 | salted_meat ×4（保质↑↑）@saltery |
| 熏鱼 | fish ×4 + 木屑 ×1 | smoked_fish ×4（保质↑↑）@smokehouse |
| 醋渍菜 | veg ×3 + vinegar ×1 + salt ×0.5 | pickled_veg ×4（保质↑）@saltery |
| 半硬质奶酪 | raw_milk ×1 + salt | hard_cheese ×0.5 + whey ×0.5（需时）@dairy_bench M |

### 3.3 文艺复兴（R）

| 配方 | 输入 | 输出 |
|---|---|---|
| 香料糖浆 | sugar ×1 + spice_blend ×0.5 | sweet_spice_syrup ×1 (风味+1 / 宴会+1) @spice_sugar_bench |
| 香料盐 | salt_fine ×1 + spice_blend ×0.5 | spiced_salt ×1 @spice_sugar_bench |
| 馅饼 pie | filling ×3 + dough ×2 | pie ×5（宴会+1）@oven R |
| 奶酪熟成 | hard_cheese | hard_cheese(quality+1)（时长长）@dairy_bench R |

---

## 4) 上下游

```
[ Agri_Brew_Works ]
   ├─ flour / flour_fine → Kitchen (面包)
   ├─ olive_oil / vegetable_oil → Kitchen (烹饪)
   ├─ honey / sugar (R) → Kitchen.spice_sugar_bench
   └─ vinegar → Kitchen.saltery (醋渍)

[ Butchery ]
   ├─ meat / poultry → Kitchen
   ├─ tallow / lard / fish_oil → Kitchen
   ├─ cracklings → Kitchen (零食)
   └─ meat_pre_cured → Kitchen.saltery (干腌完成)

[ Pasture_Shed ]
   ├─ raw_milk → Kitchen.dairy_bench
   └─ egg → Kitchen

[ Fishery ]
   ├─ fresh_fish / dried_fish → Kitchen
   ├─ oysters / mussels → Kitchen
   └─ fish_offal → Kitchen.ferment_jar (鱼酱)

[ Salt_Works ]
   └─ salt_coarse / salt_fine → Kitchen (核心耗材)

[ Chemistry_Lab ]
   ├─ spice_blend (R 进口或本地草本)
   └─ vinegar (二选一来源)

[ Logging ]
   └─ wood_chips (smokehouse 木屑) → Kitchen

[ Kitchen 输出 ]
   ├─ stew / thick_stew → 餐厅 (热食心情+)
   ├─ bread / flatbread / pie → 餐厅 / 携带食物
   ├─ salted_meat / smoked_fish / pickled_veg → 储存 / 远征食物
   ├─ fish_sauce / spiced_salt / sweet_spice_syrup → 调味料
   ├─ fresh_cheese / hard_cheese → 餐厅 / 贸易
   └─ prepared meals → 居民进食
```

---

## 5) 危害与特殊

- **保质期**: 腌/熏/醋渍 ≫ 面包/馅饼 ≫ 炖菜；装桶/密封再延（与酿造系统共享容器逻辑）
- **批量+20%**: Communal Oven (M) 给烘焙批量加成
- **腌熏成品保质 ×3**: 远征/越冬关键
- **甜香糖浆 / 香料盐 = 消耗性增益**
- **UI 建议**: 建造菜单"炖/烘焙/保藏/调味"四按钮组织

---

## 6) 与 industry md 的对应

详细 + 平衡 + 时代特点（C 鱼酱橄榄油 / M 公共炉腌熏 / R 香料糖宴）: [Cooking_System_Design.md](../industries/Cooking_System_Design.md)

（完）
