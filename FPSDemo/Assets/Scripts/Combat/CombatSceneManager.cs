using System;
using System.Collections;
using System.Collections.Generic;
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
            public string displayName;
            public string assetBundleName;
            public string assetName;
            public string configResourcesPath;
            public string editorAssetPath;

            public RunWeaponEntry(
                string displayName,
                string assetBundleName,
                string assetName,
                string configResourcesPath,
                string editorAssetPath)
            {
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
                "Default Pistol",
                DefaultPlayerBundleName,
                "PistolView",
                "WeaponConfigs/DefaultPistolWeaponConfig",
                "Assets/Art/ABRes/Player/PlayerWeapon/PistolView.prefab"),
            new RunWeaponEntry(
                "Default Assault Rifle",
                DefaultPlayerBundleName,
                "AssaultRifleView",
                "WeaponConfigs/DefaultAssaultRifleWeaponConfig",
                "Assets/Art/ABRes/Player/PlayerWeapon/AssaultRifleView.prefab"),
            new RunWeaponEntry(
                "Default Shotgun",
                DefaultPlayerBundleName,
                "ShotgunView",
                "WeaponConfigs/DefaultShotgunWeaponConfig",
                "Assets/Art/ABRes/Player/PlayerWeapon/ShotgunView.prefab")
        };

        [Header("编辑器测试")]
        [SerializeField] private bool loadSourcePrefabsInEditor = true;

        private GameObject playerInstance;
        private bool isLoading;

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

            isLoading = true;
            EnsureRuntimeManagers();

            if (openTouchCanvasOnStart)
            {
                UIManager.Instance.OpenPanelAsy<global::TounchControllerCanvas>();
            }

            if (useScenePlayerIfExists && TryUseScenePlayer(out playerInstance))
            {
                yield return LoadRunWeapons(playerInstance);
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
        }

        private void SpawnPlayer(GameObject playerObject)
        {
            Transform spawnPoint = FindSpawnPoint();
            Vector3 position = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

            playerObject.transform.SetPositionAndRotation(position, rotation);
            playerObject.name = playerAssetName;
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

            List<CarriedWeaponSlot> weaponSlots = new List<CarriedWeaponSlot>();
            for (int i = 0; i < defaultRunWeapons.Count; i++)
            {
                RunWeaponEntry entry = defaultRunWeapons[i];
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
                   && candidate.GetComponent<PlayerInventory>() != null
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
