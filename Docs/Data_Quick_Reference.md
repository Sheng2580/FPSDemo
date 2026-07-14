# FPSDemo 数据快速表

用途：快速查看当前已经应用到项目里的数据值、来源、用法和维护位置。数据层每次改武器、敌人、波次、AI Profile、ABRes key、掉落、金币、Buff、道具或存档数据时，都必须同步更新本表。

更新时间：2026-07-14

## 维护规则

- 改数据前先读 `CODEX_HANDOFF.md` 和本文件。
- 改完数据后同步更新本文件的对应表格。
- `来源文件` 写 Unity 资源或代码默认值所在文件。
- `用法` 写谁读取这个字段，避免后续不知道数据为什么存在。
- `备注` 写边界、临时状态、ABRes key 约束或后续迁移计划。
- 不在本表记录 UI 布局、输入控制、摄像机表现、动画播放逻辑。

## 武器配置

来源目录：`FPSDemo/Assets/Resources/WeaponConfigs`

| 武器 | 来源文件 | weaponId | fireMode | damage | fireInterval | magazine / reserve | reloadTime | range | attackType | spreadAngle | hitLayerMask | 用法 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Default Pistol | `DefaultPistolWeaponConfig.asset` | 1 | SemiAuto | 20 | 0.2 | 12 / 48 | 1.4 | 100 | Hitscan | 0.6 | 16321 | `WeaponController` 读取运行时武器数据 |
| Default Assault Rifle | `DefaultAssaultRifleWeaponConfig.asset` | 2 | FullAuto | 16 | 0.09 | 30 / 120 | 1.65 | 160 | Hitscan | 0.9 | 16321 | `WeaponController` 读取运行时武器数据 |
| Default Shotgun | `DefaultShotgunWeaponConfig.asset` | 3 | SemiAuto | 8 | 0.72 | 6 / 36 | 2.35 | 70 | MultiHitscan | 5.5 | 16321 | `WeaponController` 按 `pelletCount` 多射线结算 |

备注：`hitLayerMask=16321` 包含 `Default / Ground / barrier / Climbable / Enemy / Surface_Stone / Surface_Metal / Surface_Wood / Surface_Glass`。敌人伤害先用 `Enemy` Layer 快速判定，场景命中特效用 `Surface_*` Layer 快速判定，不再给大量场景物体挂 `HitSurface` MonoBehaviour。导航工具已把 `Surface_*` 层加入 NavMesh 烘焙范围，金属/木头/玻璃平台可以直接使用对应表面层。

## 武器换弹数据

| 武器 | reloadMode | reloadAmmoPerStep | reloadSingleRoundTime | canInterruptReloadByFire | 用法 |
| --- | --- | --- | --- | --- | --- |
| Default Pistol | Magazine | 12 | 1.4 | false | 整弹匣换弹，换弹结束一次补满 |
| Default Assault Rifle | Magazine | 30 | 1.65 | false | 整弹匣换弹，换弹结束一次补满 |
| Default Shotgun | SingleRound | 1 | 0.72 | true | 逐发装填，每轮加 1 发，有弹时按开火可打断换弹 |

备注：`reloadTime` 仍保留为整段换弹时长和旧配置兜底；`SingleRound` 模式下逻辑主要读取 `reloadSingleRoundTime`，后续加快换弹时应缩短单发装填时间或提高动画播放速度，而不是一次直接补满。

## 局外永久升级配置

玩家升级表正式主来源：`FPSDemo/MiniTemplate/Datas/#player_permanent_upgrade.xlsx`

武器升级表正式主来源：`FPSDemo/MiniTemplate/Datas/#weapon_permanent_upgrade.xlsx`

测试 JSON：

- `FPSDemo/MiniTemplate/GeneratedJson/tbplayer_permanent_upgrade.json`
- `FPSDemo/MiniTemplate/GeneratedJson/tbweapon_permanent_upgrade.json`

Unity 运行时优先读取：

- `FPSDemo/Assets/Resources/PlayerJson/tbplayer_permanent_upgrade.json`
- `FPSDemo/Assets/Resources/UpgradeJson/tbweapon_permanent_upgrade.json`

### 玩家永久升级

| statType | displayName | level | modifyType | value | costGold | maxLevel | 用法 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| MaxHp | 最大生命 | 1-10 | Add | 10-100 | 100-1000 | 10 | 每级增加 10 点最大生命 |
| MoveSpeed | 移动速度 | 1-10 | Add | 0.15-1.50 | 100-1000 | 10 | 同时增加走路和跑步速度 |
| JumpHeight | 跳跃高度 | 1-10 | Add | 0.05-0.50 | 100-1000 | 10 | 每级增加 0.05 跳跃高度 |
| SkillCooldownReduction | 技能冷却缩减 | 1-10 | Add | 0.03-0.30 | 100-1000 | 10 | 每级增加 3% 局外冷却缩减 上限 30% |
| EnergyGainEfficiency | 充能效率 | 1-10 | Add | 0.10-1.00 | 100-1000 | 10 | 每级增加 10% 充能效率 满级累计增加 100% |

说明：`value` 按当前等级累计值维护。Hall 当前使用统一玩家等级，一次升级会同步提升最大生命、移动速度、跳跃高度、技能冷却缩减和充能效率。默认新存档测试金币为 `10000`，用于 Hall 升级流程测试。技能冷却缩减不会改写 Dodge / Push / Grenade 的基础冷却，只在运行时参与最终冷却计算。最终跳跃高度按 `(基础跳跃高度 + 永久升级增加值) * 局内跳跃倍率` 计算。充能效率使用统一玩家等级读取，不新增独立存档等级，最终基础倍率为 `1 + Clamp(EnergyGainEfficiency, 0, 1)`。

### 武器永久升级

| weaponId | 武器 | level 范围 | damageMultiplier | magazineAdd | reserveAmmoAdd | fireIntervalMultiplier | reloadTimeMultiplier | recoilMultiplier | spreadMultiplier | costGold | 用法 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | Default Pistol | 1-10 | 1.08-1.8 | 1-8 | 4-40 | 0.985-0.85 | 0.975-0.75 | 0.975-0.75 | 0.975-0.75 | 100-1000 | 后续 Hall 武器升级读表替换当前代码兜底 |
| 2 | Default Assault Rifle | 1-10 | 1.08-1.8 | 2-20 | 10-100 | 0.985-0.85 | 0.975-0.75 | 0.975-0.75 | 0.975-0.75 | 100-1000 | 同上 |
| 3 | Default Shotgun | 1-10 | 1.08-1.8 | 1-5 | 3-30 | 0.985-0.85 | 0.975-0.75 | 0.975-0.75 | 0.975-0.75 | 100-1000 | 同上 |

说明：`damageMultiplier / fireIntervalMultiplier / reloadTimeMultiplier / recoilMultiplier / spreadMultiplier` 是该等级的最终倍率；`magazineAdd / reserveAmmoAdd` 是该等级的累计增加值。散弹枪仍保持逐发装填规则，升级表只增加弹仓容量和缩短单发装填相关时间，不能把 `reloadAmmoPerStep` 改成一次填满。

## 战斗结算存档

正式多存档目录：`Application.persistentDataPath/Saves`

Android 对应应用私有持久化目录下的 `Saves` 文件夹。新存档文件名为 `Save_yyyyMMdd_HHmmss.json`，文件名表示最后一次手动保存时间。

| 数据 | 字段 | 来源 | 用法 |
| --- | --- | --- | --- |
| `PlayerSaveData` | `gold / bestSurvivalTime / bestKillCount` | `Assets/Scripts/Character/Player/Data/PlayerSaveData.cs` | 只保存永久金币、最长存活和最高击杀纪录 |
| `CombatRunSettlementResult` | `isNewBestSurvivalTime / isNewBestKillCount / totalGold` | `Assets/Scripts/Combat/Data/CombatRunSettlementResult.cs` | `EndCanvas` 读取新纪录标记和结算后金币 |
| `PlayerSaveSlotSummary` | `FileName / FullPath / SavedAt / IsLegacy / Gold / BestSurvivalTime / BestKillCount / PlayerUpgradeLevel / SelectedSecondWeaponId` | `PlayerProgressSaveService.GetSaveSlotSummaries()` | Start 读取存档列表的只读摘要 |

结算规则：

- `PlayerProgressSaveService.SettleCombatRun(survivalSeconds, killCount, goldEarned)` 是永久进度结算入口。
- 结算入口累加最终 `goldEarned`，更新 `bestSurvivalTime / bestKillCount`，只修改当前内存会话并标记 dirty。
- 数据层不保存每局历史、`runId`、Buff、道具、弹药或其他局内统计。
- 防止同一局重复调用由 `CombatRunRecorder._completed` 保证。
- 本系统不计算本局金币，只消费 `CombatEconomyManager` 提供的最终 `goldEarned`。
- 只有 Hall 手动保存按钮调用 `CommitCurrentSession` 或 `SaveCurrentSession` 时才真正写文件。

### 多存档会话 API

| API | 返回 | 用法 |
| --- | --- | --- |
| `BeginNewSession()` | `PlayerSaveData` | Start 的开始游戏按钮创建全新内存会话，不创建文件 |
| `TryContinueLatestSession(out saveData)` | `bool` | Start 的继续游戏按钮读取保存时间最新的有效存档 |
| `GetSaveSlotSummaries()` | `IReadOnlyList<PlayerSaveSlotSummary>` | Start 的读取存档界面按时间倒序展示列表 |
| `TryLoadSession(fileName, out saveData)` | `bool` | 按列表中的文件名加载指定存档 |
| `Load()` | `PlayerSaveData` | 现有 Hall/Combat 兼容入口，优先返回当前内存会话 |
| `Reload()` | `PlayerSaveData` | 兼容旧调用，不重新读盘，不覆盖未手动保存的内存进度 |
| `Save(saveData)` | `void` | 更新当前内存会话并标记 dirty，不写文件 |
| `CommitCurrentSession(out summary)` | `bool` | Hall 手动保存入口，写入新时间戳文件并返回摘要 |
| `SaveCurrentSession(out summary)` | `bool` | `CommitCurrentSession` 同义入口 |

只读状态：`HasCurrentSession / IsCurrentSessionDirty / CurrentSaveFilePath / CurrentSaveFileName`。

保存规则：

- 新游戏首次手动保存时创建 `Save_yyyyMMdd_HHmmss.json`。
- 已加载会话再次手动保存时先写入新的时间戳文件，成功后再删除旧文件。
- 写入使用临时 `.tmp` 文件，临时文件成功移动为正式文件后才删除旧档，写入失败时保留旧档。
- 同一秒文件名冲突时顺延时间戳，保证不会覆盖其他存档。
- `PlayerProgress.json` 作为旧版存档继续出现在存档列表；加载后首次手动保存会迁移为新格式并删除旧文件。
- 损坏或无法解析的单个存档会跳过，不影响其他存档列表。
- 不会在新游戏、升级、战斗结算、切换场景或退出应用时自动创建文件。

### EndCanvas 局内展示统计

以下数据只随 `CombatRunResult` 交给结算面板展示，不写入 `PlayerProgress.json`：

| 显示项 | 建议字段 | 累计事件 | 统计口径 |
| --- | --- | --- | --- |
| 获得祝福 | `blessingSelectCount` | `PlayerEnergyBlessingSelected` | 每次成功确认一张祝福卡计 1，同一祝福重复叠层也分别计数 |
| 捡起道具 | `pickupCollectedCount` | `PickupCollected` | 玩家每次成功拾取一个道具计 1，生成或过期不计 |
| 使用子弹 | `weaponFireCount` | `WeaponFired` | 每次成功开火计 1，散弹枪一次开火仍计 1，不按弹丸数统计 |

“使用子弹”最终口径是成功开火次数，不是实际扣除弹药数。狂暴或无限弹期间只要成功触发 `WeaponFired` 仍计 1；弹夹为空、武器锁定或其他原因导致 `CanFire()` 失败时不会触发 `WeaponFired`，因此计 0。`WeaponFiredEventData` 不需要增加 `ammoConsumed`。

## 战斗评价配置

正式表：`FPSDemo/MiniTemplate/Datas/#combat_evaluation.xlsx`

生成 JSON：

- `FPSDemo/MiniTemplate/GeneratedJson/tbcombat_evaluation.json`
- `FPSDemo/Assets/Resources/CombatJson/tbcombat_evaluation.json`

| id | minSurvivalSeconds | minKillCount | evaluationText |
| --- | ---: | ---: | --- |
| 1 | 0 | 0 | 幸存者 |
| 2 | 60 | 10 | 丧尸猎手 |
| 3 | 180 | 35 | 杀戮专家 |
| 4 | 300 | 80 | 末日幸存者 |

读取规则：`CombatEvaluationConfigLoader` 按 `id` 从高到低检查，存活时间和击杀数必须同时达到阈值，返回满足条件的最高档评价。

## 武器准星数据

| 武器 | crosshairSize | crosshairMinSprayAmount | crosshairSpreadScale | crosshairFireKickAmount | crosshairFireKickDecaySpeed | 来源 | 用法 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Default Pistol | 20 | 2 | 0.8 | 1.2 | 8 | `DefaultPistolWeaponConfig.asset` | `AkilaCrosshairDriver` 读取当前 `WeaponConfig` |
| Default Assault Rifle | 30 | 2 | 0.8 | 2 | 10.5 | `DefaultAssaultRifleWeaponConfig.asset` | `AkilaCrosshairDriver` 读取当前 `WeaponConfig` |
| Default Shotgun | 42 | 2 | 1.15 | 2.4 | 6.5 | `DefaultShotgunWeaponConfig.asset` | `AkilaCrosshairDriver` 读取当前 `WeaponConfig` |

备注：`WeaponConfig.ApplyMissingDefaults()` 会为旧资源补缺省值，但正式调参以资源文件为准。

## 武器后坐力与开镜数据

| 武器 | recoilPitch | recoilYaw | viewRecoilPosition | viewRecoilRotation | viewRecoilReturnSpeed | aimFov | aimLocalPosition | aimLocalEulerAngles | aimLocalScale | 用法 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Default Pistol | -1.5 | 0.5 | `(0, -0.015, -0.08)` | `(-6, 1.5, 0)` | 18 | 50 | `(-0.084, -0.817, 0.35677)` | `(0, 0, 0)` | `(0.04, 0.04, 0.04)` | 武器后坐力、开镜姿态 |
| Default Assault Rifle | -0.32 | 0.16 | `(0, -0.004, -0.025)` | `(-1.25, 0.25, 0)` | 16 | 30 | `(-0.162, -1.517, -0.13)` | `(0, 0, 0)` | `(0.06154, 0.06154, 0.06154)` | 武器后坐力、开镜姿态 |
| Default Shotgun | -2.4 | 0.9 | `(0, -0.025, -0.12)` | `(-8, 2, 0)` | 13 | 45 | `(-0.04, -0.06, 0.3193)` | `(0.142, 1.735, -4.676)` | `(0.872284, 0.872284, 0.872284)` | 武器后坐力、开镜姿态；来自用户调好的 ShotgunView 开镜位置数据 |

## 战斗表现 Key

来源：武器配置资源 + `FPSDemo/Assets/Art/ABRes/CombatFeedback/CombatFeedbackResources.asset`

| 武器 | muzzleFlashEffectKey | muzzleSmokeEffectKey | muzzleSmokeInterval | muzzleSmokeIntensity | fireAudioKey | fireVolume | firePitchRandom | fireAudioCooldown | fireFeedbackIntensity | defaultImpactEffectKey | 用法 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Default Pistol | KriptoFX Pistol Muzzle Flash | None | 0.22 | 0.35 | Pistol_1 Fire | 1 | 0.04 | 0.03 | 1 | KriptoFX Concrete Impact | `CombatFeedbackManager` 按 key 异步加载表现资源 |
| Default Assault Rifle | KriptoFX Assault Rifle Muzzle Flash | None | 0.35 | 0 | Assault Rifle_1 Fire | 1 | 0.04 | 0.03 | 1 | KriptoFX Concrete Impact | `CombatFeedbackManager` 按 key 异步加载表现资源 |
| Default Shotgun | KriptoFX Shotgun Muzzle Flash | None | 0.45 | 0.6 | Shotgun_1 Fire | 1 | 0.04 | 0.03 | 1.25 | KriptoFX Concrete Impact | `CombatFeedbackManager` 按 key 异步加载表现资源 |

规则：武器表现只由 `WeaponConfig` 维护 key 和枪口烟雾参数，表现层只按 key 播放；不得在 `WeaponController`、`WeaponView` 或 ShotgunView prefab 中硬写特效/音效资源引用。`muzzleSmokeEffectKey` 目前保持 `None`，烟雾开关和节流仍由现有 `muzzleSmokeInterval / muzzleSmokeIntensity` 控制；本次只替换枪口火光和非敌人命中特效资源，不改烟雾逻辑。步枪默认关闭烟雾，避免高射速连续透明粒子造成 GPU 过绘；手枪保留少量烟，霰弹枪保留更明显但受节流控制的短烟。散弹枪当前使用专用 key，资源项已登记在 `CombatFeedbackResources.asset`；后续替换散弹枪音效或枪口火光时只改资源表和本快速表。

## 命中表面数据

字段来源：`WeaponConfig.HitSurfaceFeedbackConfig`

命中表面识别方式：运行时由 `CombatLayerNames` 按 Layer 解析，`Surface_Metal` 对应 `Metal`，`Surface_Wood` 对应 `Wood`，`Surface_Glass` 对应 `Glass`，`Surface_Stone` 对应 `Stone`。没有设置这些 Layer 的场景物体走 `Default`。

| surfaceType | impactEffectKey | impactAudioKey | decalKey | decalLifeTime | decalScale | 当前来源 | 用法 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Default | KriptoFX Concrete Impact | Impact Concrete | 空 | 8 | 1 | 三把武器资源相同 | 默认命中特效 |
| Stone | KriptoFX Concrete Impact | Impact Concrete | 空 | 8 | 1 | 三把武器资源相同 | 石头 / 墙体命中特效 |
| Metal | KriptoFX Metal Impact | Impact Metal | 空 | 8 | 1 | 三把武器资源相同 | 金属命中特效 |
| Wood | KriptoFX Wood Impact | Impact Wood | 空 | 8 | 1 | 三把武器资源相同 | 木头命中特效 |
| Flesh | Blood Impact | Impact Flesh | 空 | 8 | 1 | 三把武器资源相同 | 敌人血肉命中特效 |
| Glass | KriptoFX Glass Impact | Impact Glass | 空 | 8 | 1 | 三把武器资源相同 | 玻璃命中特效 |

备注：`decalKey` 目前留空，后续有弹孔贴花资源再补。`Surface_Glass` 已使用独立 `KriptoFX Glass Impact`，不再复用混凝土命中特效。

## CombatFeedback 资源表

来源：`FPSDemo/Assets/Art/ABRes/CombatFeedback/CombatFeedbackResources.asset`。兼容旧数据 key，枪口火光和非敌人命中特效当前实际绑定到 `FPSDemo/Assets/EffectCore` 的 `Bullet_BlazingRed` prefab，并打入 `combat_feedback` 包。

| key | assetBundleName | assetName | 文件 |
| --- | --- | --- | --- |
| Muzzle Flash | combat_feedback | Bullet_BlazingRed_Small_MuzzleFlare | `Assets/EffectCore/packs/StylizedProjectilePack1/prefabs/Bullet/Bullet_BlazingRed/Bullet_Small_BlazingRed/Bullet_BlazingRed_Small_MuzzleFlare.prefab` |
| Muzzle Smoke | combat_feedback | Muzzle Smoke | `Effects/Muzzle Smoke.prefab` |
| Blood Impact | combat_feedback | Blood Impact | `Effects/Blood Impact.prefab` |
| Stone Impact | combat_feedback | Bullet_BlazingRed_Medium_Impact | `Assets/EffectCore/packs/StylizedProjectilePack1/prefabs/Bullet/Bullet_BlazingRed/Bullet_Medium_BlazingRed/Bullet_BlazingRed_Medium_Impact.prefab` |
| Metal Impact | combat_feedback | Bullet_BlazingRed_Medium_Impact | `Assets/EffectCore/packs/StylizedProjectilePack1/prefabs/Bullet/Bullet_BlazingRed/Bullet_Medium_BlazingRed/Bullet_BlazingRed_Medium_Impact.prefab` |
| Wood Impact | combat_feedback | Bullet_BlazingRed_Medium_Impact | `Assets/EffectCore/packs/StylizedProjectilePack1/prefabs/Bullet/Bullet_BlazingRed/Bullet_Medium_BlazingRed/Bullet_BlazingRed_Medium_Impact.prefab` |
| KriptoFX Pistol Muzzle Flash | combat_feedback | Bullet_BlazingRed_Small_MuzzleFlare | `Assets/EffectCore/packs/StylizedProjectilePack1/prefabs/Bullet/Bullet_BlazingRed/Bullet_Small_BlazingRed/Bullet_BlazingRed_Small_MuzzleFlare.prefab` |
| KriptoFX Assault Rifle Muzzle Flash | combat_feedback | Bullet_BlazingRed_Medium_MuzzleFlare | `Assets/EffectCore/packs/StylizedProjectilePack1/prefabs/Bullet/Bullet_BlazingRed/Bullet_Medium_BlazingRed/Bullet_BlazingRed_Medium_MuzzleFlare.prefab` |
| KriptoFX Shotgun Muzzle Flash | combat_feedback | Bullet_BlazingRed_Big_MuzzleFlare | `Assets/EffectCore/packs/StylizedProjectilePack1/prefabs/Bullet/Bullet_BlazingRed/Bullet_Big_BlazingRed/Bullet_BlazingRed_Big_MuzzleFlare.prefab` |
| KriptoFX Concrete Impact | combat_feedback | Bullet_BlazingRed_Medium_Impact | `Assets/EffectCore/packs/StylizedProjectilePack1/prefabs/Bullet/Bullet_BlazingRed/Bullet_Medium_BlazingRed/Bullet_BlazingRed_Medium_Impact.prefab` |
| KriptoFX Metal Impact | combat_feedback | Bullet_BlazingRed_Medium_Impact | `Assets/EffectCore/packs/StylizedProjectilePack1/prefabs/Bullet/Bullet_BlazingRed/Bullet_Medium_BlazingRed/Bullet_BlazingRed_Medium_Impact.prefab` |
| KriptoFX Wood Impact | combat_feedback | Bullet_BlazingRed_Medium_Impact | `Assets/EffectCore/packs/StylizedProjectilePack1/prefabs/Bullet/Bullet_BlazingRed/Bullet_Medium_BlazingRed/Bullet_BlazingRed_Medium_Impact.prefab` |
| KriptoFX Glass Impact | combat_feedback | Bullet_BlazingRed_Medium_Impact | `Assets/EffectCore/packs/StylizedProjectilePack1/prefabs/Bullet/Bullet_BlazingRed/Bullet_Medium_BlazingRed/Bullet_BlazingRed_Medium_Impact.prefab` |
| Pistol_1 Fire | combat_feedback | Pistol_1 Fire | `Audio/Pistol_1 Fire.wav` |
| Assault Rifle_1 Fire | combat_feedback | Assault Rifle_1 Fire | `Audio/Assault Rifle_1 Fire.wav` |
| Shotgun_1 Fire | combat_feedback | Shotgun_1 Fire | `Audio/Shotgun_1 Fire.wav` |
| Hitmarker | combat_feedback | Hitmarker | `Audio/Hitmarker.wav` |

## CombatVolume 后处理效果配置

来源目录：`FPSDemo/Assets/Resources/CombatVolumeEffectConfigs`

| 效果 | 来源文件 | effectType | effectKey | fadeIn / hold / fadeOut | minIntensity | damageScale | missingHpScale | enableBloomPulse | 用法 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Player Damage | `PlayerDamageVolumeEffectConfig.asset` | PlayerDamage | CombatVolume_PlayerDamage | 0.08 / 0.08 / 0.32 | 0.35 | 0.08 | 0.35 | true | `CombatVolumeManager` 监听玩家受伤后按配置淡入、保持、淡出 |
| Dodge | `DodgeVolumeEffectConfig.asset` | Dodge | Skill_Dodge_SprintPulse | 0.045 / 0.04 / 0.18 | 0.65 | 0 | 0 | false | `CombatVolumeManager` 监听闪避技能后播放轻量冷色速度感脉冲 |
| Push | `PushVolumeEffectConfig.asset` | Push | Skill_Push_ImpactPulse | 0.035 / 0.05 / 0.16 | 0.55 | 0 | 0 | true | `CombatVolumeManager` 监听推人技能和推中敌人后播放冲击脉冲 |
| Grenade | `GrenadeVolumeEffectConfig.asset` | Grenade | Skill_Grenade_ExplosionPulse | 0.04 / 0.06 / 0.22 | 0.7 | 0 | 0 | true | `CombatVolumeManager` 监听手雷爆炸视觉事件后播放爆炸脉冲 |

### CombatVolume PlayerDamage 默认数值

| 字段 | 当前值 | 来源 | 备注 |
| --- | --- | --- | --- |
| colorFilter | `(1, 0.52, 0.48, 1)` | `CombatVolumeEffectConfig.CreateDefaultPlayerDamage()` | 玩家受伤红色滤镜 |
| vignetteColor | `(0.42, 0.02, 0.01, 1)` | `CombatVolumeEffectConfig.CreateDefaultPlayerDamage()` | 玩家受伤暗角颜色 |
| vignetteIntensityBoost | 0.32 | `PlayerDamageVolumeEffectConfig.asset` | 在基础 Volume 上叠加强度 |
| vignetteSmoothnessBoost | 0.12 | `PlayerDamageVolumeEffectConfig.asset` | 在基础 Volume 上叠加平滑度 |
| postExposureOffset | -0.35 | `PlayerDamageVolumeEffectConfig.asset` | 受伤瞬间降低曝光 |
| saturationOffset | -24 | `PlayerDamageVolumeEffectConfig.asset` | 受伤瞬间降低饱和度 |
| bloomIntensityBoost | 0.25 | `PlayerDamageVolumeEffectConfig.asset` | Bloom 脉冲增量 |
| bloomTintBlend | 0.35 | `PlayerDamageVolumeEffectConfig.asset` | Bloom 染色混合强度 |

规则：CombatVolume 数据层只维护淡入淡出时间、强度计算参数、颜色和后处理数值，不硬引用场景 `Volume`、材质、Shader 或 prefab 实例。表现层只按 `CombatVolumeEffectConfigAsset` 消费配置。`Dodge / Push / Grenade` 已接入 Resources 配置并由技能事件驱动。

## 玩家局内能量配置

来源目录：`FPSDemo/Assets/Resources/PlayerEnergyConfigs`

| 配置 | 来源文件 | baseRequiredEnergy | linearGrowth | quadraticGrowth | startLevel | damageToEnergyRate | 永久加成上限 | 用法 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Default Player Energy | `DefaultPlayerEnergyConfig.asset` | 100 | 15 | 2.5 | 1 | 0.05 | 100% | `PlayerEnergyRuntime` 监听 `EnemyDamaged` 后把玩家造成的伤害转为局内能量 |

## 玩家局内能量运行时数据

| 字段 | 默认值 | 来源 | 用法 |
| --- | --- | --- | --- |
| currentEnergy | 0 | `PlayerEnergyRuntimeData.InitForNewRun` | 当前局能量，范围 0 到 `maxEnergy` |
| maxEnergy | 100 | `PlayerEnergyConfig.CalculateRequiredEnergy` | 当前等级内部能量需求，按 `100 + 15n + 2.5n²` 计算，`n = level - 1` |
| level | 1 | `DefaultPlayerEnergyConfig.asset` | 当前能量等级，后续祝福选择或自动升级后递增 |
| energyGainMultiplier | 1-2 | `PlayerEnergyRuntimeData` | 基础倍率包含局外充能效率，统一玩家升级满级为 2 倍，祝福继续在运行时叠加 |
| autoLevelUp | false | `DefaultPlayerEnergyConfig.asset` | 当前先不自动升级，满能量后触发升级准备事件 |
| isLevelUpReady | false | `PlayerEnergyRuntimeData` | 满能量且等待祝福选择时为 true |

## 玩家局内能量事件

| 事件 | 参数 | 触发者 | 用法 |
| --- | --- | --- | --- |
| PlayerEnergyChanged | `PlayerEnergyChangedEventData(currentEnergy, targetEnergy, level, deltaEnergy, maxEnergy, normalizedEnergy)` | `PlayerEnergyRuntime` | HUD EnergyCentre 更新目标能量数字和进度 |
| PlayerEnergyLevelUpReady | `PlayerEnergyLevelUpEventData(level, currentEnergy, maxEnergy, autoLevelUp)` | `PlayerEnergyRuntime` | 当前等级进度达到 100% 且非自动升级时触发，后续用于打开祝福选择 |
| PlayerEnergyLevelUp | `PlayerEnergyLevelUpEventData(level, currentEnergy, maxEnergy, autoLevelUp)` | `PlayerEnergyRuntime` | 自动升级或祝福确认后触发 |

规则：HUD 只监听 `EnemyDamaged` 显示伤害数字、监听 `PlayerEnergyChanged` 显示能量变化，不计算能量成长数值。能量面板显示 `normalizedEnergy * 100`，始终是 `0%-100%`，不显示内部 `currentEnergy/maxEnergy`。能量增长来源当前监听 `EnemyDamaged`，只统计 `DamageInfo.attacker` 属于玩家的伤害；后续如果统一伤害结算事件稳定，可以切换到 `DamageResolved`。

等级需求示例：Lv1 `100`、Lv2 `118`、Lv3 `140`、Lv4 `168`、Lv5 `200`、Lv6 `238`、Lv7 `280`、Lv8 `328`、Lv9 `380`、Lv10 `438`。需求曲线使用二次函数，前期增长温和，后期逐步拉开祝福选择间隔。

运行时生命周期：`PlayerEnergyRuntime` 跨场景保留，并在每次进入 `Combat` 场景时调用 `InitRuntimeData()` 重置本局能量、等级、倍率和状态，避免从 Start/Hall 切换后监听对象被销毁。

## 正式祝福数据

正式主来源：`FPSDemo/MiniTemplate/Datas/#blessing.xlsx`

测试 JSON：`FPSDemo/MiniTemplate/GeneratedJson/tbblessing.json`

Unity 兜底入口：`FPSDemo/Assets/Scripts/Blessing/Data` 下的 `BlessingConfig / BlessingConfigAsset / BlessingConfigDatabaseAsset`，只用于当前 Resources 兜底或后续 Luban 适配，不作为正式文案和数值主来源。

| blessingId | blessingName | category | targetType | unlockEnergyLevel | unlockWave | weight | maxStack | requiredWeaponId | requiredSkillType | stat | modifyType | Normal / Plus / PlusPlus | iconKey |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1001 | 生命强化 | PlayerStat | Player | 1 | 1 | 100 | 5 | 0 | 空 | MaxHp | Add | 20 / 35 / 55 | Blessing_MaxHp |
| 1002 | 疾跑本能 | PlayerStat | Player | 1 | 1 | 90 | 5 | 0 | 空 | MoveSpeed | PercentAdd | 0.08 / 0.14 / 0.22 | Blessing_MoveSpeed |
| 1003 | 战斗兴奋 | PlayerStat | Player | 1 | 1 | 80 | 5 | 0 | 空 | EnergyGain | PercentAdd | 0.10 / 0.18 / 0.30 | Blessing_EnergyGain |
| 1101 | 枪械专注 | WeaponStat | CurrentWeapon | 1 | 1 | 100 | 5 | 0 | 空 | WeaponDamage | PercentAdd | 0.12 / 0.20 / 0.32 | Blessing_CurrentWeaponDamage |
| 1102 | 扩容弹匣 | WeaponStat | CurrentWeapon | 1 | 1 | 85 | 4 | 0 | 空 | WeaponMagazine | PercentAdd | 0.20 / 0.32 / 0.50 | Blessing_CurrentWeaponMagazine |
| 1103 | 稳定握持 | WeaponStat | CurrentWeapon | 1 | 1 | 80 | 4 | 0 | 空 | WeaponRecoil | PercentAdd | -0.15 / -0.24 / -0.36 | Blessing_CurrentWeaponRecoil |
| 1201 | 快速冷却 | SkillStat | Skill | 1 | 1 | 90 | 5 | 0 | 空 | SkillCooldownReduction | Add | 0.10 / 0.16 / 0.25 | Blessing_SkillCooldown |
| 1202 | 爆破储备 | SkillStat | Skill | 2 | 2 | 65 | 3 | 0 | Grenade | SkillMaxCount | Add | 1 / 1 / 2 | Blessing_GrenadeMaxCount |
| 1301 | 贪婪本能 | Economy | Economy | 1 | 1 | 75 | 5 | 0 | 空 | GoldGain | PercentAdd | 0.20 / 0.32 / 0.50 | Blessing_GoldGain |

字段说明：`id` 是基础祝福 ID，同一次三选一按该 ID 去重，不能出现同一基础祝福的不同 tier；`tier` 是默认展示等级，正式抽取时由等级概率表先抽本次 `BlessingTier`；`requiredWeaponId=0` 表示不限制武器；`requiredSkillType` 为空表示不限制技能；触发型字段 `triggerType / triggerChance / triggerCooldown / triggerEffectKey / triggerDamageMultiplier / triggerRadius / triggerChainCount / triggerMaxActiveCount` 已在表中预留，第一批测试数据先全部为 `None / 0`。

## 祝福等级概率

正式主来源：`FPSDemo/MiniTemplate/Datas/#blessing_tier_probability.xlsx`

测试 JSON：`FPSDemo/MiniTemplate/GeneratedJson/tbblessing_tier_probability.json`

| minEnergyLevel | Normal | Plus | PlusPlus | 说明 |
| --- | --- | --- | --- | --- |
| 1 | 100 | 0 | 0 | Lv1 |
| 2 | 80 | 20 | 0 | Lv2 |
| 3 | 65 | 30 | 5 | Lv3 |
| 4 | 50 | 40 | 10 | Lv4 |
| 5 | 40 | 45 | 15 | Lv5+ |

规则：`BlessingRoller` 根据当前能量等级先抽 `BlessingTier`，再按武器、技能、解锁等级、最大层数过滤基础祝福，最后按 `weight` 生成三选一候选。祝福效果后续写入数值修正层，不直接改 `PlayerBaseConfig / WeaponConfig / PlayerSkillConfig`。

## 玩家技能配置

正式主来源：`FPSDemo/MiniTemplate/Datas/#player_skill_config.xlsx`

冷却规则来源：`FPSDemo/MiniTemplate/Datas/#player_skill_rules.xlsx`

生成 JSON：

- `FPSDemo/MiniTemplate/GeneratedJson/tbplayer_skill_config.json`
- `FPSDemo/MiniTemplate/GeneratedJson/tbplayer_skill_rules.json`

Unity 运行时优先读取：

- `FPSDemo/Assets/Resources/PlayerJson/tbplayer_skill_config.json`
- `FPSDemo/Assets/Resources/PlayerJson/tbplayer_skill_rules.json`

`FPSDemo/Assets/Resources/PlayerSkillConfigs` 下的三个 ScriptableObject 只作为 JSON 读取失败时的兜底。

| 技能 | 来源文件 | skillId | skillType | cooldown | duration | lockWeaponDuringCast | postProcessKey | 用法 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Dodge | `tbplayer_skill_config.json` | 1 | Dodge | 3.5 | 0.22 | true | Skill_Dodge_SprintPulse | 闪避位移和短暂无敌 |
| Push | `tbplayer_skill_config.json` | 2 | Push | 6 | 0.45 | true | Skill_Push_ImpactPulse | 近战推敌解围 |
| Grenade | `tbplayer_skill_config.json` | 3 | Grenade | 8 | 0.45 | true | Skill_Grenade_ExplosionPulse | 投掷炸弹范围伤害 |

冷却计算规则：

- 局外永久升级冷却缩减上限为 30%
- 局内祝福冷却缩减上限为 30%
- 最终总冷却缩减上限为 60%
- 最终冷却为 `基础冷却 * (1 - 永久缩减 - 祝福缩减)`
- 满缩减时 Dodge / Push / Grenade 最低冷却分别为 `1.4 / 2.4 / 3.2` 秒

## 玩家技能数值

| 技能 | distance / detect / radius / angle | damage | knockback | stun | count | resource / animation key | 备注 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Dodge | `dodgeDistance 4.2` | 0 | 0 | `invincibleDuration 0.18` | 无 | `fovEffectKey Skill_Dodge_FOV` | `collisionDisableDuration 0.12`，表现只读取 key |
| Push | `detectDistance 2.2 / detectRadius 0.85 / detectAngle 80` | 5 | 6.5 | 0.45 | `maxHitCount 5` | `Melee Weapon_1|Attack 1` / `Melee Weapon_1|Attack 2` | 只保存动画 key，不硬引用动画资源 |
| Grenade | `explosionRadius 4.5` | 80 | 8 | 0.35 | `initialCount 2 / maxCount 3` | `projectileResourceKey Gernade` / `throwAnimationKey Grenade_1 |Throw` | `explosionDelay 1.2`，`throwForce 13`，`throwUpForce 2.5` |

技能数据边界：

- `PlayerSkillConfig` 只保存数值和资源 key，不硬引用场景对象、Volume、材质、Shader 或 prefab 实例。
- `PlayerSkillRuntimeData` 只保存单局临时状态，不写回配置资源。
- 表现层后续消费 `animationKey / effectKey / audioKey / fovEffectKey / postProcessKey / cameraShakeKey`。
- 武器系统只读取技能释放锁定状态，不直接持有技能配置。

## 局内道具配置

正式主来源：`FPSDemo/MiniTemplate/Datas/#pickup_item.xlsx`

测试 JSON：`FPSDemo/MiniTemplate/GeneratedJson/tbpickup_item.json`

Unity 运行时优先读取：`FPSDemo/Assets/Resources/PickupJson/tbpickup_item.json`

Unity 数据脚本：`FPSDemo/Assets/Scripts/Pickup/Data`

| id | itemName | itemType | assetBundleName | assetName | weight | unlockWave | lifeTime | pickupRadius | healValue | ammoAmount | grenadeAmount | berserkDuration | tipColorKey | postProcessKey | 用法 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1001 | 小型医疗包 | Heal | prop_runtime | HpProp | 100 | 1 | 18 | 1.4 | 30 | 0 | 0 | 0 | Heal | 空 | 拾取后恢复生命 |
| 1002 | 弹药补给 | Ammo | prop_runtime | BulletProp | 90 | 1 | 18 | 1.4 | 0 | 30 | 0 | 0 | Ammo | 空 | 拾取后给玩家本局携带的所有武器补充备弹 |
| 1003 | 炸弹补给 | Grenade | prop_runtime | bombProp | 65 | 2 | 18 | 1.4 | 0 | 0 | 1 | 0 | Grenade | 空 | 拾取后增加手雷技能数量 |
| 1004 | 狂暴药剂 | Berserk | prop_runtime | RageProp | 55 | 2 | 16 | 1.4 | 0 | 0 | 0 | 6 | Berserk | Pickup_Berserk_SpeedLines | 拾取后进入短时狂暴效果，有弹不扣弹，弹夹为 0 不能开枪 |

字段说明：

- `assetBundleName / assetName` 只保存 AB 资源 key，不硬引用 prefab 实例。`assetName` 必须和 `Assets/Art/ABRes/Prop` 下 prefab 文件名一致。
- `descriptionTemplate` 当前为 `恢复生命 +{0}`、`获得子弹 +{0}`、`获得炸弹 +{0}`、`狂暴时间 +{0}秒`，表现层按道具类型选择对应数值填入。
- `PickupItemConfigLoader` 读取顺序为 `Resources/PickupJson` -> `StreamingAssets` -> `MiniTemplate/GeneratedJson`。
- `PickupItemRuntimeData` 只保存单局生成时间、剩余存活时间和拾取状态，不写回配置。
- 道具事件已接入 `GameEvent.cs`：`PickupSpawned`、`PickupCollected`、`PickupExpired`、`PickupTipRequested`、`PlayerBerserkChanged`。
- 狂暴后处理只打开或关闭 Combat Volume Profile 中名为 `SpeedLines` 的组件，不调整强度参数。

## 敌人配置

来源目录：`FPSDemo/Assets/Resources/EnemyConfigs`

| 敌人 | 来源文件 | enemyId | prefabResourceKey | behaviorTreeKey | aiProfileKey | maxHealth | moveSpeed | attackDamage | attackDistance | attackInterval | detectionRange | gold | blessingEnergy | 备注 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Zombie Skeleton OneHanded | `NormalZombieEnemyConfig.asset` | 1001 | Enemy_ZombieSkeleton_LOD2 | ZombieMelee | NormalZombieAI | 100 | 2.2 | 10 | 1.4 | 1.2 | 30 | 1 | 1 | 基础杂兵，真实 ABRes prefab |
| Zombie Nerd OneHanded | `FastZombieEnemyConfig.asset` | 1002 | Enemy_ZombieNerd_LOD2 | ZombieMelee | FastZombieAI | 90 | 2.45 | 9 | 1.35 | 1.1 | 32 | 2 | 1 | 普通杂兵，真实 ABRes prefab |
| Zombie Old Crone OneHanded | `EliteZombieEnemyConfig.asset` | 1003 | Enemy_ZombieOldCrone_LOD2 | ZombieMelee | EliteZombieAI | 180 | 1.45 | 14 | 1.45 | 1.45 | 28 | 3 | 2 | 慢速厚血普通怪，真实 ABRes prefab |

## 敌人受击与动画数据

| 敌人 | hitStunDuration | hitReactionCooldown | hitKnockbackDistance | hitKnockbackDuration | locomotionTransition | attackTransition | hitTransition | deathTransition | recoverTransition |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Zombie Skeleton OneHanded | 0.09 | 0.2 | 0.08 | 0.06 | 0.18 | 0.1 | 0.14 | 0.18 | 0.18 |
| Zombie Nerd OneHanded | 0.08 | 0.18 | 0.07 | 0.055 | 0.17 | 0.1 | 0.13 | 0.17 | 0.16 |
| Zombie Old Crone OneHanded | 0.12 | 0.24 | 0.1 | 0.08 | 0.2 | 0.12 | 0.16 | 0.2 | 0.2 |

动画状态名按真实 prefab 对应角色前缀维护：

| 敌人 | idleStateName | walkStateName | runStateName | attackStateName | damageStateName | deathStateName |
| --- | --- | --- | --- | --- | --- | --- |
| Zombie Skeleton OneHanded | ZombieSkeleton_OneHanded_Idle | ZombieSkeleton_OneHanded_Walk | ZombieSkeleton_OneHanded_Run | ZombieSkeleton_OneHanded_Attack_1 | ZombieSkeleton_OneHanded_Damage | ZombieSkeleton_OneHanded_Death |
| Zombie Nerd OneHanded | ZombieNerd_OneHanded_Idle | ZombieNerd_OneHanded_Walk | ZombieNerd_OneHanded_Run | ZombieNerd_OneHanded_Attack_1 | ZombieNerd_OneHanded_Damage | ZombieNerd_OneHanded_Death |
| Zombie Old Crone OneHanded | ZombieOldCrone_OneHanded_Idle | ZombieOldCrone_OneHanded_Walk | ZombieOldCrone_OneHanded_Run | ZombieOldCrone_OneHanded_Attack_1 | ZombieOldCrone_OneHanded_Damage | ZombieOldCrone_OneHanded_Death |

## 敌人部位倍率

| 敌人 | Head | Body | Arm | Leg | 来源 | 用法 |
| --- | --- | --- | --- | --- | --- | --- |
| Zombie Skeleton OneHanded | 2 | 1 | 0.75 | 0.6 | `NormalZombieEnemyConfig.asset` | `EnemyHitBox` / 伤害结算 |
| Zombie Nerd OneHanded | 2 | 1 | 0.75 | 0.6 | `FastZombieEnemyConfig.asset` | `EnemyHitBox` / 伤害结算 |
| Zombie Old Crone OneHanded | 2 | 1 | 0.75 | 0.6 | `EliteZombieEnemyConfig.asset` | `EnemyHitBox` / 伤害结算 |

## Enemy AI Profile

来源目录：`FPSDemo/Assets/Resources/EnemyAIProfiles`

| profile | near / mid / far | think near / mid / far / sleep | rootMotion near / mid | agent near / mid | animatorLodDistance | attackPriority | surroundRadius |
| --- | --- | --- | --- | --- | --- | --- | --- |
| NormalZombieAI | 8 / 18 / 35 | 0.12 / 0.35 / 1 / 3 | true / false | true / true | 24 | 1 | 1.8 |
| FastZombieAI | 10 / 22 / 40 | 0.08 / 0.25 / 0.8 / 3 | true / false | true / true | 26 | 2 | 1.5 |
| EliteZombieAI | 9 / 20 / 36 | 0.1 / 0.3 / 0.9 / 3.2 | true / false | true / true | 24 | 4 | 2.2 |

## 波次配置

来源目录：`FPSDemo/Assets/Resources/EnemyWaves`

| 波次模板 | 时间兜底 | difficultyTierIndex | wavesPerDifficultyTier | waveTotalSpawnCount | waveTotalSpawnGrowth | waveClearDelay | waitForAvailableSpawnSlot | spawnInterval | batch | batchGrowth/min | maxBatch | sceneMax | maxNear | maxAgent | maxAttackers | 用法 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Wave01 | 0-60 | 1 | 3 | 12 | 3 | 5 | true | 3 | 1 | 0 | 1 | 10 | 8 | 10 | 2 | 前 3 波基础刷怪模板 |
| Wave02 | 60-180 | 2 | 3 | 20 | 4 | 5 | true | 2.5 | 1 | 0.5 | 2 | 16 | 12 | 14 | 3 | 第 4-6 波中期刷怪模板 |
| Wave03 | 180+ | 3 | 3 | 30 | 6 | 5 | true | 2 | 2 | 0.5 | 4 | 24 | 16 | 18 | 4 | 第 7 波后期刷怪模板，后续可作为最高档兜底 |

正式波次消费规则：

- `waveTotalSpawnCount` 是该难度档第一波总刷怪数。
- `waveTotalSpawnGrowth` 是同一难度档内每推进 1 波增加的刷怪数。
- `wavesPerDifficultyTier` 当前统一为 3，表示每 3 波切换一个难度档。
- `EnemyWaveConfig.GetTotalSpawnCountForWave(absoluteWaveIndex)` 可根据第 N 波返回该波总刷怪数。
- `EnemyWaveConfig.GetFirstWaveIndexInDifficultyTier()` 可把旧时间段模板映射到该难度档首个绝对波次。
- `EnemyWaveConfig.GetResolvedSpawnEntriesForWave(absoluteWaveIndex)` 可根据第 N 波返回已筛选、已计算权重和倍率的刷新池。
- `EnemyRuntimeStats.TryCreateFromWave(wave, absoluteWaveIndex, waveElapsedTime, ...)` 可直接按权重生成最终运行时敌人数值。
- `waveClearDelay` 当前统一为 5 秒，表现层清完当前波后等待 5 秒进入下一波。
- `waitForAvailableSpawnSlot = true` 表示当前波还没刷完但场上敌人达到 `sceneMaxEnemyCount` 时，不丢弃剩余生成数，等待敌人死亡回池后继续补刷。

## 波次候选敌人算法配置

`EnemySpawnEntry` 现在不是逐波手填结果，而是候选敌人配置。控制层只传 `absoluteWaveIndex / waveElapsedTime`，数据层负责筛选候选敌人、计算最终权重和倍率。

| 模板 | 敌人 | unlock / min / max | baseWeight | weightGrowth/wave | weightGrowth/tier | maxWeight | maxAlive | base H/D/S/G | growth/wave H/D/S/G | growth/tier H/D/S/G | max H/D/S/G |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Wave01 | Zombie Skeleton OneHanded | 1 / 1 / 0 | 100 | 0 | 0 | 100 | 8 | 1 / 1 / 1 / 1 | 0.04 / 0.02 / 0.01 / 0.03 | 0 / 0 / 0 / 0 | 1.15 / 1.08 / 1.03 / 1.1 |
| Wave01 | Zombie Nerd OneHanded | 1 / 1 / 0 | 100 | 0 | 0 | 100 | 4 | 1 / 1 / 1 / 1 | 0.04 / 0.02 / 0.01 / 0.03 | 0 / 0 / 0 / 0 | 1.12 / 1.08 / 1.04 / 1.1 |
| Wave01 | Zombie Old Crone OneHanded | 1 / 1 / 0 | 100 | 0 | 0 | 100 | 2 | 1 / 1 / 1 / 1 | 0.04 / 0.02 / 0 / 0.03 | 0 / 0 / 0 / 0 | 1.12 / 1.08 / 1 / 1.1 |
| Wave02 | Zombie Skeleton OneHanded | 1 / 4 / 0 | 70 | -2 | 0 | 70 | 12 | 1.1 / 1.05 / 1 / 1.1 | 0.05 / 0.03 / 0.01 / 0.05 | 0 / 0 / 0 / 0 | 1.35 / 1.2 / 1.05 / 1.3 |
| Wave02 | Zombie Nerd OneHanded | 4 / 4 / 0 | 45 | 2 | 0 | 55 | 6 | 1 / 1 / 1 / 1 | 0.05 / 0.03 / 0.01 / 0.04 | 0 / 0 / 0 / 0 | 1.35 / 1.18 / 1.05 / 1.2 |
| Wave02 | Zombie Old Crone OneHanded | 3 / 4 / 0 | 24 | 2 | 0 | 34 | 3 | 1 / 1 / 1 / 1 | 0.05 / 0.03 / 0 / 0.04 | 0 / 0 / 0 / 0 | 1.35 / 1.18 / 1 / 1.25 |
| Wave03 | Zombie Skeleton OneHanded | 1 / 7 / 0 | 50 | -2 | 0 | 50 | 16 | 1.3 / 1.15 / 1.05 / 1.25 | 0.06 / 0.035 / 0.01 / 0.05 | 0.08 / 0.04 / 0 / 0.05 | 1.8 / 1.5 / 1.15 / 1.8 |
| Wave03 | Zombie Nerd OneHanded | 4 / 7 / 0 | 48 | 1 | 0 | 58 | 9 | 1.25 / 1.12 / 1.05 / 1.2 | 0.06 / 0.035 / 0.01 / 0.05 | 0.08 / 0.04 / 0 / 0.05 | 1.8 / 1.5 / 1.15 / 1.7 |
| Wave03 | Zombie Old Crone OneHanded | 3 / 7 / 0 | 34 | 2 | 3 | 50 | 5 | 1.25 / 1.12 / 1 / 1 | 0.06 / 0.035 / 0 / 0.08 | 0.1 / 0.04 / 0 / 0.1 | 2 / 1.5 / 1 / 2 |

说明：`H/D/S/G` 分别表示 `healthMultiplier / damageMultiplier / moveSpeedMultiplier / goldMultiplier`。`maxWaveIndex = 0` 表示不自动退场；如果某类早期怪后续需要退出，可配置为具体波次。

快速示例：

| 绝对波次 | 使用模板 | 可刷敌人和大致权重 |
| --- | --- | --- |
| 1 | Wave01 | Skeleton 100 |
| 2 | Wave01 | Skeleton 100，Nerd 35 |
| 3 | Wave01 | Skeleton 100，Nerd 40，Old Crone 12 |
| 4 | Wave02 | Skeleton 70，Nerd 45，Old Crone 24 |
| 7 | Wave03 | Skeleton 50，Nerd 48，Old Crone 34 |
| 10 | Wave03 作为后期兜底 | Skeleton 44，Nerd 51，Old Crone 40 |

## ABRes 敌人 Prefab

来源：`FPSDemo/Assets/Art/ABRes/Enemies/Prefabs`

| prefabResourceKey | 文件 | 当前使用者 | 备注 |
| --- | --- | --- | --- |
| Enemy_ZombieSkeleton_LOD2 | `Enemy_ZombieSkeleton_LOD2.prefab` | Zombie Skeleton OneHanded | 已在 `enemy_prefabs` 包，视觉模型替换为 Polygon Slobber，当前不自动绑定手持武器 |
| Enemy_ZombieNerd_LOD2 | `Enemy_ZombieNerd_LOD2.prefab` | Zombie Nerd OneHanded | 已在 `enemy_prefabs` 包，视觉模型替换为 Polygon Wretch，key 与文件名一致 |
| Enemy_ZombieOldCrone_LOD2 | `Enemy_ZombieOldCrone_LOD2.prefab` | Zombie Old Crone OneHanded | 已在 `enemy_prefabs` 包，视觉模型替换为 Polygon Brute，当前不自动绑定手持武器 |

视觉替换规则：敌人 `prefabResourceKey` 没有变化，刷怪、波次、对象池仍读取同一批 ABRes prefab。Prefab 内部只替换可视模型、Animator 绑定、CharacterController 尺寸和部位判定盒；手持武器由用户后续手动绑定。重生成入口为 `FPSDemo/Enemy/替换普通敌人为 PolygonBossZombies 模型`，工具文件在 `FPSDemo/Assets/Scripts/Editor/PolygonBossZombieEnemyBuildTools.cs`。

## ABRes 敌人与场景表现材质

敌人旧材质来源：`FPSDemo/Assets/Art/ABRes/Enemies/Materials`，AssetBundle：`enemy_prefabs`

| 用途 | ABRes 材质或资源 | 当前引用 |
| --- | --- | --- |
| Skeleton 视觉 | `Assets/PolygonBossZombies/Prefabs/SM_Chr_ZombieBoss_Slobber_01.prefab` | `Enemy_ZombieSkeleton_LOD2.prefab` |
| Nerd 视觉 | `Assets/PolygonBossZombies/Prefabs/SM_Chr_ZombieBoss_Wretch_01.prefab` | `Enemy_ZombieNerd_LOD2.prefab` |
| Old Crone 视觉 | `Assets/PolygonBossZombies/Prefabs/SM_Chr_ZombieBoss_Brute_01.prefab` | `Enemy_ZombieOldCrone_LOD2.prefab` |
| 敌人手持武器 | 用户后续手动绑定 | 当前三种运行时普通敌人 prefab 不再由生成工具自动挂武器 |
| 旧 Zombie Collection 材质 | `AB_ZombieSkeleton_*` / `AB_ZombieNerd_*` / `AB_ZombieOldCrone_*` | 目前不再被三种运行时普通敌人 prefab 直接引用，后续保留为旧资源或回退参考 |

敌人材质移动端优化：当前敌人材质已从“强补光强边缘光”调整为更脏、更压暗的移动端版本。皮肤 / 身体材质当前 `_MinVisibility 0.50`、`_AmbientLift 0.46`、`_ColdRimStrength 0.34`、`_RimDamp 0.22`、`_EyeGlowIntensity 0.72`、`_WoundGlowIntensity 0.45`、`_DirtAmount 0.24`、`_Wetness 0.18`；衣服材质当前 `_MinVisibility 0.54`、`_AmbientLift 0.50`、`_ColdRimStrength 0.24`、`_CharacterFillStrength 0.12`、`_FocusLift 0.11`、`_WoundGlowIntensity 0`；头发材质当前 `_MinVisibility 0.52`、`_AmbientLift 0.48`、`_ColdRimStrength 0.22`；敌人手持武器当前 `_MinVisibility 0.44`、`_AmbientLift 0.40`、`_ColdRimStrength 0.28`。敌人 shader 保留主光方向、SH 环境光、雾融合、法线和脏污/湿润细节，但已取消实时主光阴影采样和阴影变体，适配烘焙场景与大量敌人同屏。

敌人 Prefab 渲染开关：`Enemy_ZombieSkeleton_LOD2`、`Enemy_ZombieNerd_LOD2`、`Enemy_ZombieOldCrone_LOD2` 的渲染器已关闭动态投影、接收实时阴影、运动向量、Skinned Motion Vectors 和反射探针；保留 Light Probe，用于移动敌人在烘焙场景中获得环境光过渡。当前 PolygonBossZombies 视觉替换工具也会对新模型和手持道具应用同一套低成本渲染开关。后续如果需要近景 Boss 投影，建议单独给 Boss 开启，不要恢复到普通杂兵 prefab。

场景材质来源：`FPSDemo/Assets/Art/ABRes/SceneMaterials`，AssetBundle：`scene_materials`

| 用途 | ABRes 材质 | Shader |
| --- | --- | --- |
| 地面沙土 | `Materials/AB_M_Sand_Grounded.mat` | `Shaders/S_ABRes_ScenePBRHigh.shader` |
| 石头 / 岩面 | `Materials/AB_M_Rock_Grounded.mat` | `Shaders/S_ABRes_ScenePBRHigh.shader` |
| 柱子 / 石柱 | `Materials/AB_M_Pillar_a_Grounded.mat` | `Shaders/S_ABRes_ScenePBRHigh.shader` |
| 墙体 Trim | `Materials/AB_M_Trim01_Grounded.mat` / `AB_M_Trim02_Grounded.mat` / `AB_M_Trim02_a_Grounded.mat` / `AB_M_Trim02_a_Tint_Grounded.mat` | `Shaders/S_ABRes_ScenePBRHigh.shader` |
| 金属 / 木材 | `Materials/AB_M_Metals_Grounded.mat` / `AB_M_Wood_Grounded.mat` | `Shaders/S_ABRes_ScenePBRHigh.shader` |

备注：场景表现材质是 ABRes 副本，不再直接改 Hivemind 原始材质。当前场景 shader 已从错误的假光照方案改为 URP PBR 真实受光方案，材质只负责 BaseColor / Normal / RMA（R=粗糙度、G=金属度、B=AO）/ 湿润脏污参数，阴影和高光交给 URP 灯光。Combat 场景和 GladitorArena 场景 prefab 的核心地面、柱子、墙体材质引用已切到 ABRes 副本；如果后续新增场景 prefab，也应优先引用 `Assets/Art/ABRes/SceneMaterials/Materials` 中的材质。

暗部可见度参数：`S_ABRes_ScenePBRHigh.shader` 使用 `_ShadowLift / _ShadowFloor / _ShadowTint` 防止阴影区域死黑。当前 ABRes 场景材质统一为 `_AOIntensity 0.62`、`_Contrast 0.92`、`_DirtAmount 0.08`、`_ShadowLift 0.58`、`_ShadowFloor 0.13`、`_ShadowTint (0.42, 0.46, 0.40)`。Combat 场景配合环境光、SSAO 和 Volume 降低压黑，避免墙角、柱背、地面遮挡处丢失信息。

## 当前临时约束

- 三种普通近战怪现在分别使用 `Enemy_ZombieSkeleton_LOD2`、`Enemy_ZombieNerd_LOD2`、`Enemy_ZombieOldCrone_LOD2`，数据层已按 ABRes prefab 文件名维护 `prefabResourceKey`。
- 如果后续表现层重新生成或改名 prefab，必须同步更新 `EnemyConfig.prefabResourceKey` 和本快速表，保持 key 与 `Assets/Art/ABRes/Enemies/Prefabs` 文件名一致。
- 火把怪灼烧、毒液远程怪、精英复杂技能本轮只保留为后续预留，不实现。
- 所有武器表现 key 必须先存在于 `CombatFeedbackResources.asset`，再写入武器配置。
- 本表是快速读取表，不替代源码和 Unity Inspector。最终运行值以资源文件和 `ApplyMissingDefaults()` 后的运行时数据为准。

## 敌人波次正式字段

字段来源：`EnemyWaveConfig`，资源位置：`FPSDemo/Assets/Resources/EnemyWaves`

| 字段 | 当前状态 | 用法 |
| --- | --- | --- |
| difficultyTierIndex | 已接入 | 表示这份 Wave 资源对应第几个难度档 |
| wavesPerDifficultyTier | 已接入 | 表示每几个正式波次使用同一难度档，当前为 3 |
| waveTotalSpawnCount | 已接入 | 本难度档第一波总生成数量，用于判断本波是否已经刷完 |
| waveTotalSpawnGrowth | 已接入 | 同一难度档内每推进 1 波增加的总生成数量 |
| waveClearDelay | 已接入 | 本波清空后等待多久进入下一波，当前统一 5 秒 |
| waitForAvailableSpawnSlot | 已接入 | 达到 `sceneMaxEnemyCount` 时等待敌人死亡回池后继续补刷 |
| sceneMaxEnemyCount | 已接入 | 场上敌人总数量上限 |
| maxActiveAgentCount | 已接入 | 当前波次最大追击/活跃 Agent 数量 |
| maxAttackersCount | 已接入 | 当前波次同时攻击玩家的敌人上限 |
| EnemySpawnEntry.unlockWaveIndex | 已接入 | 候选敌人从第几波开始可能进入刷新池 |
| EnemySpawnEntry.minWaveIndex | 已接入 | 当前难度模板内的最低出现波次，用于模板复用 |
| EnemySpawnEntry.maxWaveIndex | 已接入 | 可选退出波次，0 表示不退出 |
| EnemySpawnEntry.baseWeight / weightGrowthPerWave / weightGrowthPerDifficultyTier / maxWeight | 已接入 | 数据层计算当前波次最终刷新权重 |
| EnemySpawnEntry.*MultiplierGrowthPerWave / *MultiplierGrowthPerDifficultyTier / max*Multiplier | 已接入 | 数据层计算生命、伤害、速度、金币倍率 |
| EnemyRuntimeStats.absoluteWaveIndex / waveElapsedTime / difficultyTierIndex | 已接入 | 表现层调试当前敌人来自第几波和哪个难度档 |
| EnemyRuntimeStats.candidateFinalWeight / resolved*Multiplier | 已接入 | 表现层调试当前敌人最终权重和倍率 |

待后续控制层或 AI 数据继续接入：`softlockTimeout`、`stuckTeleportRadius`、`allowRecycleWhenUnreachable`。当前 `maxActiveAgentCount` 已被控制层临时用作最大追击玩家数量。拿到追击名额的怪直接 Run，没拿到名额的怪 Walk 到玩家外圈徘徊等待。
