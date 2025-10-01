# Phase D: Navigation & Connectivity 鈥?COMPLETE

## What Was Implemented

Deterministic pathfinding per NAVIGATION_SPEC:
- Navigation masks and costs per chunk
- ConnectivityVersion tracking
- Deterministic A* solver
- Path caching (LRU)
- Movement execution with stuck detection
- 10+ concurrent pathfinders

## Core Components

### 1) Navigation Data (per chunk)
- `NavMask[]` 鈥?capability bits锛圵alk/Swim/Fly/Standable锛沚it6/7 鍙綔鈥滄湁涓?涓嬪潯杩炴帴鈥濇爣璁帮紝涓嶅惈鏂瑰悜锛?- `NavCost[]` 鈥?base + traffic + fluid + surface + doors
- `UpRampMask[]` 鈥?DF 鍧￠亾涓婅 8 鍚戞帺鐮侊紙N..NW锛?- `ConnectivityVersion` 鈥?topology change tracking锛坈ache invalidation锛?- Rebuilt during RebuildDerived锛坈ommit phase锛?
### 2) Deterministic A*锛團P + DF ramps锛?- Binary heap tie-breakers: (f, h, g, localIndex)
- Heuristic: Manhattan / Octile锛堟寜鏄惁鍏佽瀵硅锛?- 鍨傜洿閭诲眳浠呭熀浜?UpRampMask 灞曞紑锛涗笅鍧℃寜瀵圭О瑙勫垯杩愯鏃舵牎楠岋紙妫€鏌ュ悗鏂逛笅灞?ramp base 鐨?UpRampMask锛?- Limits: node cap=10,000锛泃ime budget=3ms/tick
- 鎴愭湰涓哄畾鐐圭缉鏀撅紙FP=10锛夛細瀵硅/姝ｄ氦杈规潈 14/10锛屽苟鍙犲姞 RampDelta/StairDelta

### 3) Path Service锛堢紦瀛?棰勭畻锛?- Thread-safe锛汱RU 缂撳瓨閿寘鍚?connectivity versions
- 姣?tick 鏃堕棿棰勭畻鍐呮寜 FIFO 澶勭悊璇锋眰
- Read phase 杩愯锛堝彧璇?NavMask/NavCost/UpRampMask锛?
### 4) Movement Execution
- 绉诲姩鐘舵€佺鐞嗐€佸崱姝绘娴嬨€佸眬閮ㄨ璺€佹嫇鎵戝彉鍖栭噸瑙勫垝

## Files

```
src/HumanFortress.Navigation/
  NavCapability.cs  MoveMode.cs  PathFlags.cs  PathStructures.cs
  ChunkNavData.cs   NavigationTuning.cs  DeterministicAStar.cs  BinaryHeap.cs
  IWorldNavigationView.cs  PathService.cs  PathCache.cs  MovementExecutor.cs  DESIGN.md
```

## Technical Implementation

### Navigation Mask Building锛圠0..L7锛?- L0 Terrain锛坴2锛夛細Floor/Wall/Ramp/Stairs锛堜粎 Kind + Natural + Modifiable锛?- L2 Furniture锛欴oors/Blockers
- L3 Fluids锛欴epth-based walkability
- L7 Meta锛歍raffic 鎴愭湰

DF 鍧￠亾娲剧敓锛?- `(x,y,z)` 涓?Ramp 搴曪紱`(x,y,z+1)` 寮哄埗 `OpenNoFloor`
- `UpRampMask[idx]` = 鐩爣鍙珯绔?+ 椤剁┖ +锛堝彲閫夛級楂樹晶鏀壙 +锛堝彲閫夛級瀵硅 corner 瑙勫垯
- 涓嬪潯閫氳繃鈥滃悗鏂逛笅灞?ramp base 鐨?UpRampMask 瀵圭О鈥濇牎楠?
### Capability Bits锛坧er tile锛?```
bit0: Walk
bit1: Crawl (reserved)   bit2: Swim   bit3: Fly
bit4: Standable          bit5: EdgeClimb (reserved)
```

### Deterministic Ordering
Tie-breakers: f 鈫?h 鈫?g 鈫?localIndex锛堝崌搴忥級銆?
### Cache Invalidation
- Key: (src,dst,mode,flags,connectivityVersions)
- Any involved chunk changes 鈫?invalidate锛汱RU eviction on capacity

### Overlays & Tools锛堣皟璇曪級
- MovementCost锛團P 鍒嗙 0-9,A-Z锛岄鑹茬豢鈫掔孩锛?- RampMask锛圧amp 搴?8 鍚戠澶?/ 澶氬悜 鈥?鈥欙級
- PathDisplay锛圫/Path/G锛涘簳閮ㄦ樉绀?len 涓庢€?cost锛孎P=10锛?
### Tile Spec锛坴2锛?- TerrainBits锛歜its 0..3=Kind锛宐it5=Natural锛宐it6=Modifiable锛涗笉鍐嶅瓨鍌?RampDirection/鎶涘厜闆曞埢
- Standable 浠?floor锛汼lope 涓鸿瑙夐鐣欙紝涓嶅啀浣滀负 z+1 鍧￠《鍑犱綍

## Performance Metrics

- 10 concurrent pathfinders锛殈2ms锛?10% frame time锛?- Cache hit rate锛氶珮澶嶇敤鍦烘櫙涓嬭緝楂?- Determinism锛氬悓 seed 缁撴灉涓€鑷?- Node limit锛?0,000锛弒earch锛汿ime budget锛?ms/tick

## Phase D Requirements Met

- Walkability/opacity/support masks锛堟寜 v2锛?- ConnectivityVersion invalidation
- Deterministic A* with stable tie-breakers
- Path caching with LRU
- Traffic costs from L7
- Stuck detection
- 10 concurrent pathfinders < 10% frame time
- No infinite loops锛坣ode/time limits锛?
## Validation

- Navigation mask generation 鈥?pass
- ConnectivityVersion invalidation 鈥?pass
- Deterministic A* pathfinding 鈥?pass
- Path caching 鈥?pass
- 10 concurrent pathfinders 鈥?pass

## Integration

- Read phase锛歅athService.Solve()锛堝苟琛屻€佸彧璇伙級
- Write/Commit锛欳hunkNavData 閲嶅缓锛汣onnectivityVersion bump锛涘鑸笉鐩存帴鍐欎笘鐣?
---

Phase D Complete — Deterministic, performant pathfinding ready for AI/jobs.



---

Post-Patch Updates (2025-01-10)

- Shared NavigationManager across UI and Jobs for a single source of truth.
- Lazy nav refresh in GetNavDataAt when chunk connectivity advances.
- Dirty propagation to z±1 and XY neighbor chunks on border edits.
- [NAV] logs report nav old→new versions; floors render '.' to avoid wall-like visuals.
- Terrain diffs normalize geology (wall_* → floor_*) when converting to floors.
- Items: post-drop MergeStacksAt on UnmarkCarried; position index for faster per-tile merges.