# FPSDemo 数据快速表

用途：快速查看当前已经应用到项目里的数据值、来源、用法和维护位置。数据层每次改武器、敌人、波次、AI Profile、ABRes key、掉落、金币、Buff、道具或存档数据时，都必须同步更新本表。

更新时间：2026-07-10

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
| Default Pistol | `DefaultPistolWeaponConfig.asset` | 1 | SemiAuto | 20 | 0.2 | 12 / 48 | 1.4 | 100 | Hitscan | 0.6 | 115 | `WeaponController` 读取运行时武器数据 |
| Default Assault Rifle | `DefaultAssaultRifleWeaponConfig.asset` | 2 | FullAuto | 16 | 0.09 | 30 / 120 | 1.65 | 160 | Hitscan | 0.9 | 115 | `WeaponController` 读取运行时武器数据 |

## 武器准星数据

| 武器 | crosshairSize | crosshairMinSprayAmount | crosshairSpreadScale | crosshairFireKickAmount | crosshairFireKickDecaySpeed | 来源 | 用法 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Default Pistol | 20 | 2 | 0.8 | 1.2 | 8 | `DefaultPistolWeaponConfig.asset` | `AkilaCrosshairDriver` 读取当前 `WeaponConfig` |
| Default Assault Rifle | 30 | 2 | 0.8 | 2 | 10.5 | `DefaultAssaultRifleWeaponConfig.asset` | `AkilaCrosshairDriver` 读取当前 `WeaponConfig` |

备注：`WeaponConfig.ApplyMissingDefaults()` 会为旧资源补缺省值，但正式调参以资源文件为准。

## 武器后坐力与开镜数据

| 武器 | recoilPitch | recoilYaw | viewRecoilPosition | viewRecoilRotation | viewRecoilReturnSpeed | aimFov | aimLocalPosition | aimLocalScale | 用法 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Default Pistol | -1.5 | 0.5 | `(0, -0.015, -0.08)` | `(-6, 1.5, 0)` | 18 | 50 | `(-0.084, -0.817, 0.35677)` | `(0.04, 0.04, 0.04)` | 武器后坐力、开镜姿态 |
| Default Assault Rifle | -0.32 | 0.16 | `(0, -0.004, -0.025)` | `(-1.25, 0.25, 0)` | 16 | 30 | `(-0.162, -1.517, -0.13)` | `(0.06154, 0.06154, 0.06154)` | 武器后坐力、开镜姿态 |

## 战斗表现 Key

来源：武器配置资源 + `FPSDemo/Assets/Art/ABRes/CombatFeedback/CombatFeedbackResources.asset`

| 武器 | muzzleFlashEffectKey | muzzleSmokeEffectKey | fireAudioKey | fireVolume | firePitchRandom | fireAudioCooldown | fireFeedbackIntensity | defaultImpactEffectKey | 用法 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Default Pistol | Muzzle Flash | Muzzle Smoke | Pistol_1 Fire | 1 | 0.04 | 0.03 | 1 | Stone Impact | `CombatFeedbackManager` 按 key 异步加载表现资源 |
| Default Assault Rifle | Muzzle Flash | Muzzle Smoke | Assault Rifle_1 Fire | 1 | 0.04 | 0.03 | 1 | Stone Impact | `CombatFeedbackManager` 按 key 异步加载表现资源 |

## 命中表面数据

字段来源：`WeaponConfig.HitSurfaceFeedbackConfig`

| surfaceType | impactEffectKey | impactAudioKey | decalKey | decalLifeTime | decalScale | 当前来源 | 用法 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Default | Stone Impact | 空 | 空 | 8 | 1 | 两把武器资源相同 | 默认命中特效 |
| Stone | Stone Impact | 空 | 空 | 8 | 1 | 两把武器资源相同 | 石头 / 墙体命中特效 |
| Metal | Metal Impact | 空 | 空 | 8 | 1 | 两把武器资源相同 | 金属命中特效 |
| Wood | Wood Impact | 空 | 空 | 8 | 1 | 两把武器资源相同 | 木头命中特效 |
| Flesh | Blood Impact | 空 | 空 | 8 | 1 | 两把武器资源相同 | 敌人血肉命中特效 |

备注：`impactAudioKey` 和 `decalKey` 目前留空，后续有命中音效或弹孔贴花资源再补。

## CombatFeedback ABRes 资源表

来源：`FPSDemo/Assets/Art/ABRes/CombatFeedback`

| key | assetBundleName | assetName | 文件 |
| --- | --- | --- | --- |
| Muzzle Flash | combat_feedback | Muzzle Flash | `Effects/Muzzle Flash.prefab` |
| Muzzle Smoke | combat_feedback | Muzzle Smoke | `Effects/Muzzle Smoke.prefab` |
| Blood Impact | combat_feedback | Blood Impact | `Effects/Blood Impact.prefab` |
| Stone Impact | combat_feedback | Stone Impact | `Effects/Stone Impact.prefab` |
| Metal Impact | combat_feedback | Metal Impact | `Effects/Metal Impact.prefab` |
| Wood Impact | combat_feedback | Wood Impact | `Effects/Wood Impact.prefab` |
| Pistol_1 Fire | combat_feedback | Pistol_1 Fire | `Audio/Pistol_1 Fire.wav` |
| Assault Rifle_1 Fire | combat_feedback | Assault Rifle_1 Fire | `Audio/Assault Rifle_1 Fire.wav` |
| Hitmarker | combat_feedback | Hitmarker | `Audio/Hitmarker.wav` |

## 敌人配置

来源目录：`FPSDemo/Assets/Resources/EnemyConfigs`

| 敌人 | 来源文件 | enemyId | prefabResourceKey | behaviorTreeKey | aiProfileKey | maxHealth | moveSpeed | attackDamage | attackDistance | attackInterval | detectionRange | gold | blessingEnergy | 备注 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Zombie Skeleton | `NormalZombieEnemyConfig.asset` | 1001 | Enemy_ZombieSkeleton_LOD2 | ZombieMelee | NormalZombieAI | 100 | 2.2 | 10 | 1.4 | 1.2 | 30 | 1 | 1 | 当前真实 ABRes prefab |
| Fast Zombie | `FastZombieEnemyConfig.asset` | 1002 | Enemy_ZombieSkeleton_LOD2 | ZombieMelee | FastZombieAI | 70 | 3.2 | 8 | 1.35 | 0.95 | 34 | 2 | 1 | 临时复用 Skeleton prefab，不同数值 |
| Elite Zombie | `EliteZombieEnemyConfig.asset` | 1003 | Enemy_ZombieSkeleton_LOD2 | ZombieMelee | EliteZombieAI | 280 | 1.7 | 22 | 1.8 | 1.6 | 32 | 8 | 5 | 临时复用 Skeleton prefab，不同数值 |

## 敌人受击与动画数据

| 敌人 | hitStunDuration | hitReactionCooldown | hitKnockbackDistance | hitKnockbackDuration | locomotionTransition | attackTransition | hitTransition | deathTransition | recoverTransition |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Zombie Skeleton | 0.09 | 0.2 | 0.08 | 0.06 | 0.18 | 0.1 | 0.14 | 0.18 | 0.18 |
| Fast Zombie | 0.07 | 0.16 | 0.06 | 0.05 | 0.16 | 0.09 | 0.12 | 0.16 | 0.14 |
| Elite Zombie | 0.12 | 0.28 | 0.12 | 0.08 | 0.2 | 0.12 | 0.16 | 0.22 | 0.2 |

动画状态名当前三类敌人都使用：

| 字段 | 值 |
| --- | --- |
| idleStateName | ZombieSkeleton_OneHanded_Idle |
| walkStateName | ZombieSkeleton_OneHanded_Walk |
| runStateName | ZombieSkeleton_OneHanded_Run |
| attackStateName | ZombieSkeleton_OneHanded_Attack_1 |
| damageStateName | ZombieSkeleton_OneHanded_Damage |
| deathStateName | ZombieSkeleton_OneHanded_Death |

## 敌人部位倍率

| 敌人 | Head | Body | Arm | Leg | 来源 | 用法 |
| --- | --- | --- | --- | --- | --- | --- |
| Zombie Skeleton | 2 | 1 | 0.75 | 0.6 | `NormalZombieEnemyConfig.asset` | `EnemyHitBox` / 伤害结算 |
| Fast Zombie | 2 | 1 | 0.75 | 0.6 | `FastZombieEnemyConfig.asset` | `EnemyHitBox` / 伤害结算 |
| Elite Zombie | 1.6 | 1 | 0.75 | 0.6 | `EliteZombieEnemyConfig.asset` | `EnemyHitBox` / 伤害结算 |

## Enemy AI Profile

来源目录：`FPSDemo/Assets/Resources/EnemyAIProfiles`

| profile | near / mid / far | think near / mid / far / sleep | rootMotion near / mid | agent near / mid | animatorLodDistance | attackPriority | surroundRadius |
| --- | --- | --- | --- | --- | --- | --- | --- |
| NormalZombieAI | 8 / 18 / 35 | 0.12 / 0.35 / 1 / 3 | true / false | true / true | 24 | 1 | 1.8 |
| FastZombieAI | 10 / 22 / 40 | 0.08 / 0.25 / 0.8 / 3 | true / false | true / true | 26 | 2 | 1.5 |
| EliteZombieAI | 9 / 20 / 36 | 0.1 / 0.3 / 0.9 / 3.2 | true / false | true / true | 24 | 4 | 2.2 |

## 波次配置

来源目录：`FPSDemo/Assets/Resources/EnemyWaves`

| 波次 | 时间 | spawnInterval | batch | batchGrowth/min | maxBatch | sceneMax | maxNear | maxAgent | maxAttackers | 用法 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Wave01 | 0-60 | 3 | 1 | 0 | 1 | 10 | 8 | 10 | 2 | 前 1 分钟基础刷怪 |
| Wave02 | 60-180 | 2.5 | 1 | 0.5 | 2 | 16 | 12 | 14 | 3 | 中期加入快速敌人数值 |
| Wave03 | 180+ | 2 | 2 | 0.5 | 4 | 24 | 16 | 18 | 4 | 后期加入精英敌人数值 |

## 波次 Entry 权重

| 波次 | 敌人 | weight | maxAlive | healthMultiplier | damageMultiplier | moveSpeedMultiplier | goldMultiplier |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Wave01 | NormalZombie | 100 | 10 | 1 | 1 | 1 | 1 |
| Wave02 | NormalZombie | 80 | 12 | 1.2 | 1.1 | 1 | 1.1 |
| Wave02 | FastZombie | 20 | 4 | 1 | 1 | 1 | 1 |
| Wave03 | NormalZombie | 60 | 16 | 1.5 | 1.25 | 1.05 | 1.25 |
| Wave03 | FastZombie | 30 | 8 | 1.2 | 1.15 | 1.05 | 1.2 |
| Wave03 | EliteZombie | 10 | 3 | 1 | 1 | 1 | 1 |

## ABRes 敌人 Prefab

来源：`FPSDemo/Assets/Art/ABRes/Enemies/Prefabs`

| prefabResourceKey | 文件 | 当前使用者 | 备注 |
| --- | --- | --- | --- |
| Enemy_ZombieSkeleton_LOD2 | `Enemy_ZombieSkeleton_LOD2.prefab` | Normal / Fast / Elite 三种数据敌人 | 当前唯一真实 ABRes 敌人 prefab |

## 当前临时约束

- Fast / Elite 是不同数据敌人，但暂时复用 `Enemy_ZombieSkeleton_LOD2`。
- 如果后续新增 `Enemy_ZombieNerd_LOD2.prefab` 或 `Enemy_ZombieBrute_LOD2.prefab` 到 ABRes，必须同步改本表和对应 EnemyConfig 资源。
- 所有武器表现 key 必须先存在于 `CombatFeedbackResources.asset`，再写入武器配置。
- 本表是快速读取表，不替代源码和 Unity Inspector。最终运行值以资源文件和 `ApplyMissingDefaults()` 后的运行时数据为准。
