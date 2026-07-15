using System;
using System.Collections;
using System.Collections.Generic;
using PlayerData;
using UnityEngine;
using UnityEngine.SceneManagement;
using Weapon;
using Weapon.Data;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Combat
{
    public class CombatSceneManager : MonoBehaviour
    {
        private const string CombatSceneName = "Combat";
        private const string DefaultPlayerBundleName = "player_runtime";
        private const string DefaultPlayerAssetName = "Player";
        private const string DefaultSpawnPointName = "PlayerBirthplace";
        private const string DefaultWeaponRootName = "WeaponViewRoot";
        private const string DefaultPlayerEditorPath = "Assets/Art/ABRes/Player/Player.prefab";

        private static CombatSceneManager activeInstance;
        private static bool sceneLoadedSubscribed;

        [Serializable]
        private class RunWeaponEntry
        {
            public int weaponId;
            public string displayName;
            public string assetBundleName;
            public string assetName;
            public string configResourcesPath;
            public string editorAssetPath;

            public RunWeaponEntry(
                int weaponId,
                string displayName,
                string assetBundleName,
                string assetName,
                string configResourcesPath,
                string editorAssetPath)
            {
                this.weaponId = weaponId;
                this.displayName = displayName;
                this.assetBundleName = assetBundleName;
                this.assetName = assetName;
                this.configResourcesPath = configResourcesPath;
                this.editorAssetPath = editorAssetPath;
            }
        }

        [Header("场景")]
        [SerializeField] private string combatSceneName = CombatSceneName;
        [SerializeField] private string spawnPointName = DefaultSpawnPointName;
        [SerializeField] private bool openTouchCanvasOnStart = true;
        [SerializeField] private bool openHUDCanvasOnStart = true;
        [SerializeField] private bool openCombatCanvasOnStart = true;
        [SerializeField] private bool openTipCanvasOnStart = true;
        [SerializeField] private bool openEnemyLifebarCanvasOnStart = true;

        [Header("玩家")]
        [SerializeField] private string playerAssetBundleName = DefaultPlayerBundleName;
        [SerializeField] private string playerAssetName = DefaultPlayerAssetName;
        [SerializeField] private string playerEditorAssetPath = DefaultPlayerEditorPath;
        [SerializeField] private string weaponRootName = DefaultWeaponRootName;
        [SerializeField] private int defaultWeaponIndex;
        [SerializeField] private bool useScenePlayerIfExists = true;

        [Header("本局默认武器")]
        [SerializeField]
        private List<RunWeaponEntry> defaultRunWeapons = new List<RunWeaponEntry>
        {
            new RunWeaponEntry(
                1,
                "Default Pistol",
                DefaultPlayerBundleName,
                "PistolView",
                "WeaponConfigs/DefaultPistolWeaponConfig",
                "Assets/Art/ABRes/Player/PlayerWeapon/PistolView.prefab"),
            new RunWeaponEntry(
                2,
                "Default Assault Rifle",
                DefaultPlayerBundleName,
                "AssaultRifleView",
                "WeaponConfigs/DefaultAssaultRifleWeaponConfig",
                "Assets/Art/ABRes/Player/PlayerWeapon/AssaultRifleView.prefab"),
            new RunWeaponEntry(
                3,
                "Default Shotgun",
                DefaultPlayerBundleName,
                "ShotgunView",
                "WeaponConfigs/DefaultShotgunWeaponConfig",
                "Assets/Art/ABRes/Player/PlayerWeapon/ShotgunView.prefab")
        };

        [Header("编辑器测试")]
        [SerializeField] private bool loadSourcePrefabsInEditor;

        private GameObject playerInstance;
        private bool isLoading;
        private bool isGrantingMissingPrimaryWeapon;
        private CombatEconomyManager combatEconomyManager;
        private CombatRunRecorder combatRunRecorder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            activeInstance = null;
            sceneLoadedSubscribed = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapAfterSceneLoad()
        {
            SubscribeSceneLoaded();
            EnsureManagerForActiveScene();
        }

        private static void SubscribeSceneLoaded()
        {
            if (sceneLoadedSubscribed)
            {
                return;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            sceneLoadedSubscribed = true;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == CombatSceneName)
            {
                EnsureManagerForActiveScene();
            }
        }

        private static void EnsureManagerForActiveScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.name != CombatSceneName)
            {
                return;
            }

            if (activeInstance != null)
            {
                return;
            }

            activeInstance = FindObjectOfType<CombatSceneManager>();
            if (activeInstance != null)
            {
                return;
            }

            GameObject managerObject = new GameObject(nameof(CombatSceneManager));
            activeInstance = managerObject.AddComponent<CombatSceneManager>();
        }

        private void Awake()
        {
            if (activeInstance != null && activeInstance != this)
            {
                Destroy(gameObject);
                return;
            }

            activeInstance = this;
        }

        private IEnumerator Start()
        {
            if (!IsCombatScene())
            {
                yield break;
            }

            yield return LoadCombatRoutine();
        }

        private IEnumerator LoadCombatRoutine()
        {
            if (isLoading || playerInstance != null)
            {
                yield break;
            }

            Time.timeScale = 1f;
            isLoading = true;
            EnsureRuntimeManagers();

            if (openTouchCanvasOnStart)
            {
                UIManager.Instance.OpenPanelAsy<global::TounchControllerCanvas>();
            }

            if (openHUDCanvasOnStart)
            {
                UIManager.Instance.OpenPanelAsy<global::HUDCanvas>();
            }

            if (openCombatCanvasOnStart)
            {
                UIManager.Instance.OpenPanelAsy<global::CombatCanvas>();
            }

            if (openTipCanvasOnStart)
            {
                UIManager.Instance.OpenPanelAsy<global::TipCanvas>();
            }

            if (openEnemyLifebarCanvasOnStart)
            {
                UIManager.Instance.OpenPanelAsy<global::EnemyLifebarCanvas>();
            }

            if (useScenePlayerIfExists && TryUseScenePlayer(out playerInstance))
            {
                yield return LoadRunWeapons(playerInstance);
                StartCombatRun(playerInstance);
                isLoading = false;
                Debug.Log("[CombatScene] 已使用场景里的玩家并配置默认武器", playerInstance);
                yield break;
            }

            GameObject loadedPlayer = null;
            yield return LoadPrefabInstance(
                playerAssetBundleName,
                playerAssetName,
                playerEditorAssetPath,
                prefabInstance => loadedPlayer = prefabInstance);

            if (loadedPlayer == null)
            {
                Debug.LogError("[CombatScene] 玩家预制体加载失败", this);
                isLoading = false;
                yield break;
            }

            playerInstance = loadedPlayer;
            SpawnPlayer(playerInstance);

            yield return LoadRunWeapons(playerInstance);
            StartCombatRun(playerInstance);
            isLoading = false;
            Debug.Log("[CombatScene] 战斗场景玩家和默认武器加载完成", this);
        }

        private bool TryUseScenePlayer(out GameObject scenePlayer)
        {
            scenePlayer = FindScenePlayer();
            return scenePlayer != null;
        }

        private void EnsureRuntimeManagers()
        {
            _ = GameInputManger.Instance;
            _ = ABManager.Instance;
            _ = UIManager.Instance;
            _ = MultiTimerManager.Instance;
            global::Pickup.PickupManager.EnsureForCurrentScene();
        }

        private void StartCombatRun(GameObject playerObject)
        {
            PlayerController player = playerObject != null ? playerObject.GetComponent<PlayerController>() : null;
            if (player == null)
            {
                Debug.LogError("[CombatScene] 玩家缺少 PlayerController 无法启动战斗统计", playerObject);
                return;
            }

            combatEconomyManager?.Dispose();
            combatRunRecorder?.Dispose();
            combatEconomyManager = new CombatEconomyManager(player);
            combatRunRecorder = new CombatRunRecorder(player);
        }

        private void OnDestroy()
        {
            combatRunRecorder?.Dispose();
            combatRunRecorder = null;
            combatEconomyManager?.Dispose();
            combatEconomyManager = null;

            if (activeInstance == this)
            {
                activeInstance = null;
            }
        }

        private void SpawnPlayer(GameObject playerObject)
        {
            Transform spawnPoint = FindSpawnPoint();
            Vector3 position = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

            playerObject.transform.SetPositionAndRotation(position, rotation);
            playerObject.name = playerAssetName;
        }

        public static bool TryGrantMissingPrimaryWeapon(PlayerInventory inventory)
        {
            if (inventory == null)
            {
                return false;
            }

            CombatSceneManager manager = activeInstance != null
                ? activeInstance
                : FindObjectOfType<CombatSceneManager>();
            return manager != null && manager.BeginGrantMissingPrimaryWeapon(inventory);
        }

        private bool BeginGrantMissingPrimaryWeapon(PlayerInventory inventory)
        {
            if (isGrantingMissingPrimaryWeapon || inventory == null || !IsCombatScene())
            {
                return false;
            }

            int weaponId = ResolveMissingPrimaryWeaponId(inventory);
            if (weaponId <= 0)
            {
                return false;
            }

            RunWeaponEntry entry = FindRunWeaponEntry(weaponId);
            if (entry == null)
            {
                Debug.LogWarning($"[CombatScene] 找不到额外主武器配置 Id={weaponId}", this);
                return false;
            }

            isGrantingMissingPrimaryWeapon = true;
            StartCoroutine(GrantMissingPrimaryWeaponRoutine(inventory, entry));
            return true;
        }

        private IEnumerator GrantMissingPrimaryWeaponRoutine(PlayerInventory inventory, RunWeaponEntry entry)
        {
            Transform weaponRoot = inventory != null
                ? FindChildRecursive(inventory.transform, weaponRootName)
                : null;
            if (weaponRoot == null)
            {
                Debug.LogWarning($"[CombatScene] 玩家缺少武器挂点 {weaponRootName}", inventory);
                isGrantingMissingPrimaryWeapon = false;
                yield break;
            }

            GameObject weaponObject = null;
            yield return LoadPrefabInstance(
                entry.assetBundleName,
                entry.assetName,
                entry.editorAssetPath,
                prefabInstance => weaponObject = prefabInstance);

            if (inventory == null || weaponObject == null)
            {
                if (weaponObject != null)
                {
                    Destroy(weaponObject);
                }

                Debug.LogWarning($"[CombatScene] 额外主武器加载失败 {entry.assetName}", this);
                isGrantingMissingPrimaryWeapon = false;
                yield break;
            }

            WeaponView weaponView = AttachWeaponView(weaponObject, weaponRoot, entry.assetName);
            if (weaponView == null)
            {
                Destroy(weaponObject);
                Debug.LogWarning($"[CombatScene] 额外主武器缺少 WeaponView {entry.assetName}", this);
                isGrantingMissingPrimaryWeapon = false;
                yield break;
            }

            WeaponConfigAsset configAsset = LoadWeaponConfig(entry.configResourcesPath);
            CarriedWeaponSlot slot = new CarriedWeaponSlot();
            slot.ConfigureRuntimeWeapon(entry.displayName, weaponView, configAsset);
            if (!inventory.TryAddRunWeapon(slot))
            {
                Destroy(weaponObject);
                Debug.LogWarning($"[CombatScene] 额外主武器加入背包失败 {entry.assetName}", this);
            }
            else
            {
                Debug.Log($"[CombatScene] 已获得额外主武器 {entry.displayName}", inventory);
            }

            isGrantingMissingPrimaryWeapon = false;
        }

        private static int ResolveMissingPrimaryWeaponId(PlayerInventory inventory)
        {
            bool hasRifle = inventory != null && inventory.HasWeapon(2);
            bool hasShotgun = inventory != null && inventory.HasWeapon(3);
            if (hasRifle && hasShotgun)
            {
                return 0;
            }

            return hasRifle ? 3 : 2;
        }

        private IEnumerator LoadRunWeapons(GameObject playerObject)
        {
            PlayerInventory inventory = playerObject.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                Debug.LogError("[CombatScene] 玩家缺少 PlayerInventory 无法配置本局武器", playerObject);
                yield break;
            }

            Transform weaponRoot = FindChildRecursive(playerObject.transform, weaponRootName);
            if (weaponRoot == null)
            {
                Debug.LogError($"[CombatScene] 玩家缺少武器挂点 {weaponRootName}", playerObject);
                yield break;
            }

            ClearWeaponViews(weaponRoot);

            List<RunWeaponEntry> runWeapons = ResolveRunWeaponsFromSave();
            List<CarriedWeaponSlot> weaponSlots = new List<CarriedWeaponSlot>();
            for (int i = 0; i < runWeapons.Count; i++)
            {
                RunWeaponEntry entry = runWeapons[i];
                if (entry == null)
                {
                    continue;
                }

                GameObject weaponObject = null;
                yield return LoadPrefabInstance(
                    entry.assetBundleName,
                    entry.assetName,
                    entry.editorAssetPath,
                    prefabInstance => weaponObject = prefabInstance);

                if (weaponObject == null)
                {
                    Debug.LogError($"[CombatScene] 武器预制体加载失败 {entry.assetName}", this);
                    continue;
                }

                WeaponView weaponView = AttachWeaponView(weaponObject, weaponRoot, entry.assetName);
                if (weaponView == null)
                {
                    Destroy(weaponObject);
                    Debug.LogError($"[CombatScene] 武器预制体缺少 WeaponView {entry.assetName}", this);
                    continue;
                }

                WeaponConfigAsset configAsset = LoadWeaponConfig(entry.configResourcesPath);
                CarriedWeaponSlot slot = new CarriedWeaponSlot();
                slot.ConfigureRuntimeWeapon(entry.displayName, weaponView, configAsset);
                weaponSlots.Add(slot);
            }

            inventory.ConfigureRunWeapons(weaponSlots, defaultWeaponIndex);
        }

        private List<RunWeaponEntry> ResolveRunWeaponsFromSave()
        {
            List<RunWeaponEntry> result = new List<RunWeaponEntry>(2);
            RunWeaponEntry pistol = FindRunWeaponEntry(1);
            if (pistol != null)
            {
                result.Add(pistol);
            }

            PlayerSaveData saveData = PlayerProgressSaveService.Load();
            int secondWeaponId = PlayerProgressSaveService.NormalizeSecondWeaponId(saveData.selectedSecondWeaponId);
            RunWeaponEntry secondWeapon = FindRunWeaponEntry(secondWeaponId) ?? FindRunWeaponEntry(2);
            if (secondWeapon != null && ResolveWeaponId(secondWeapon) != 1)
            {
                result.Add(secondWeapon);
            }

            if (result.Count > 0)
            {
                return result;
            }

            for (int i = 0; i < defaultRunWeapons.Count && result.Count < 2; i++)
            {
                if (defaultRunWeapons[i] != null)
                {
                    result.Add(defaultRunWeapons[i]);
                }
            }

            return result;
        }

        private RunWeaponEntry FindRunWeaponEntry(int weaponId)
        {
            if (defaultRunWeapons == null)
            {
                return null;
            }

            for (int i = 0; i < defaultRunWeapons.Count; i++)
            {
                RunWeaponEntry entry = defaultRunWeapons[i];
                if (entry != null && ResolveWeaponId(entry) == weaponId)
                {
                    return entry;
                }
            }

            return null;
        }

        private int ResolveWeaponId(RunWeaponEntry entry)
        {
            if (entry == null)
            {
                return 0;
            }

            if (entry.weaponId > 0)
            {
                return entry.weaponId;
            }

            WeaponConfigAsset configAsset = LoadWeaponConfig(entry.configResourcesPath);
            return configAsset != null && configAsset.Config != null ? configAsset.Config.weaponId : 0;
        }

        private static void ClearWeaponViews(Transform weaponRoot)
        {
            WeaponView[] existingViews = weaponRoot.GetComponentsInChildren<WeaponView>(true);
            for (int i = existingViews.Length - 1; i >= 0; i--)
            {
                WeaponView weaponView = existingViews[i];
                if (weaponView == null)
                {
                    continue;
                }

                Destroy(weaponView.gameObject);
            }
        }

        private WeaponView AttachWeaponView(GameObject weaponObject, Transform weaponRoot, string weaponName)
        {
            weaponObject.name = string.IsNullOrEmpty(weaponName) ? weaponObject.name : weaponName;
            weaponObject.transform.SetParent(weaponRoot, false);
            ResetLocalTransform(weaponObject.transform);
            return weaponObject.GetComponent<WeaponView>() ?? weaponObject.GetComponentInChildren<WeaponView>(true);
        }

        private WeaponConfigAsset LoadWeaponConfig(string resourcesPath)
        {
            if (string.IsNullOrEmpty(resourcesPath))
            {
                return null;
            }

            WeaponConfigAsset configAsset = Resources.Load<WeaponConfigAsset>(resourcesPath);
            if (configAsset == null)
            {
                Debug.LogWarning($"[CombatScene] 找不到武器数据 {resourcesPath}", this);
            }

            return configAsset;
        }

        private IEnumerator LoadPrefabInstance(
            string assetBundleName,
            string assetName,
            string editorAssetPath,
            Action<GameObject> onLoaded)
        {
#if UNITY_EDITOR
            if (loadSourcePrefabsInEditor)
            {
                GameObject editorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(editorAssetPath);
                onLoaded?.Invoke(editorPrefab != null ? Instantiate(editorPrefab) : null);
                yield break;
            }
#endif

            bool loaded = false;
            GameObject prefabInstance = null;
            ABManager.Instance.LoadResAsync<GameObject>(assetBundleName, assetName, loadedObject =>
            {
                if (this == null || !IsCombatScene())
                {
                    if (loadedObject != null)
                    {
                        Destroy(loadedObject);
                    }

                    loaded = true;
                    return;
                }

                prefabInstance = loadedObject;
                loaded = true;
            });

            yield return new WaitUntil(() => loaded);
            onLoaded?.Invoke(prefabInstance);
        }

        private bool IsCombatScene()
        {
            return SceneManager.GetActiveScene().name == combatSceneName;
        }

        private Transform FindSpawnPoint()
        {
            GameObject spawnObject = GameObject.Find(spawnPointName);
            if (spawnObject == null)
            {
                Debug.LogWarning($"[CombatScene] 找不到玩家出生点 {spawnPointName} 使用原点", this);
            }

            return spawnObject != null ? spawnObject.transform : null;
        }

        private GameObject FindScenePlayer()
        {
            GameObject namedPlayer = GameObject.Find(playerAssetName);
            if (IsValidScenePlayer(namedPlayer))
            {
                return namedPlayer;
            }

            PlayerController playerController = FindObjectOfType<PlayerController>();
            if (playerController != null && IsValidScenePlayer(playerController.gameObject))
            {
                return playerController.gameObject;
            }

            GameObject taggedPlayer = GameObject.FindWithTag("Player");
            return IsValidScenePlayer(taggedPlayer) ? taggedPlayer : null;
        }

        private bool IsValidScenePlayer(GameObject candidate)
        {
            return candidate != null
                   && candidate.scene.IsValid()
                   && candidate.GetComponent<PlayerController>() != null
                   && candidate.GetComponent<PlayerMotor>() != null
                   && candidate.GetComponent<PlayerInventory>() != null
                   && candidate.GetComponent<CharacterController>() != null
                   && FindChildRecursive(candidate.transform, weaponRootName) != null;
        }

        private static void ResetLocalTransform(Transform target)
        {
            if (target == null)
            {
                return;
            }

            target.localPosition = Vector3.zero;
            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one;
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            if (root.name == childName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindChildRecursive(root.GetChild(i), childName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }
}
