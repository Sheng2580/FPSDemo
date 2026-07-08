using UnityEngine;
using Weapon.Data;

namespace Weapon
{
    public class WeaponView : MonoBehaviour
    {
        private static readonly int AmmoHash = Animator.StringToHash("Ammo");
        private static readonly int IsReloadingHash = Animator.StringToHash("Is Reloading");
        private static readonly int ADSAmountHash = Animator.StringToHash("ADS Amount");
        private static readonly int SprintAmountHash = Animator.StringToHash("Sprint Amount");

        [SerializeField] private Animator animator;
        [SerializeField] private Transform muzzlePoint;
        [SerializeField] private Transform shellPoint;

        private WeaponConfig _config;
        private Vector3 _defaultLocalPosition;
        private Quaternion _defaultLocalRotation;
        private Vector3 _recoilPositionOffset;
        private Vector3 _recoilRotationOffset;

        public Animator Animator => animator;
        public Transform MuzzlePoint => muzzlePoint;
        public Transform ShellPoint => shellPoint;

        private void Awake()
        {
            CacheReferences();
            CacheDefaultTransform();
        }

        private void Reset()
        {
            CacheReferences();
            CacheDefaultTransform();
        }

        private void Update()
        {
            UpdateViewRecoil();
        }

        public void Init(WeaponConfig config)
        {
            _config = config;
            CacheReferences();
            CacheDefaultTransform();
            PlayIdle();
        }

        public void PlayIdle()
        {
            CrossFade(_config?.idleStateName, 0f);
        }

        public void PlayEquip()
        {
            CrossFade(_config?.equipStateName, _config != null ? _config.equipTransition : 0f);
        }

        public void PlayFire()
        {
            CrossFade(_config?.fireStateName, _config != null ? _config.fireTransition : 0f);
            ApplyViewRecoil();
        }

        public void PlayReload()
        {
            CrossFade(_config?.reloadStateName, _config != null ? _config.reloadTransition : 0f);
        }

        public void SetAmmo(int ammo)
        {
            if (animator == null)
            {
                return;
            }

            animator.SetInteger(AmmoHash, ammo);
        }

        public void SetReloading(bool isReloading)
        {
            if (animator == null)
            {
                return;
            }

            animator.SetBool(IsReloadingHash, isReloading);
        }

        public void SetADSAmount(float value)
        {
            if (animator == null)
            {
                return;
            }

            animator.SetFloat(ADSAmountHash, value);
        }

        public void SetSprintAmount(float value)
        {
            if (animator == null)
            {
                return;
            }

            animator.SetFloat(SprintAmountHash, value);
        }

        private void CacheReferences()
        {
            animator ??= GetComponent<Animator>();
            animator ??= GetComponentInChildren<Animator>(true);
            muzzlePoint ??= FindChildRecursive(transform, "MuzzlePoint");
            shellPoint ??= FindChildRecursive(transform, "ShellPoint");
        }

        private void CacheDefaultTransform()
        {
            _defaultLocalPosition = transform.localPosition;
            _defaultLocalRotation = transform.localRotation;
        }

        private void ApplyViewRecoil()
        {
            if (_config == null)
            {
                return;
            }

            _recoilPositionOffset += _config.viewRecoilPosition;
            _recoilRotationOffset += _config.viewRecoilRotation;
        }

        private void UpdateViewRecoil()
        {
            float returnSpeed = _config != null ? _config.viewRecoilReturnSpeed : 18f;
            float lerpSpeed = Mathf.Max(0f, returnSpeed) * Time.deltaTime;

            _recoilPositionOffset = Vector3.Lerp(_recoilPositionOffset, Vector3.zero, lerpSpeed);
            _recoilRotationOffset = Vector3.Lerp(_recoilRotationOffset, Vector3.zero, lerpSpeed);

            transform.localPosition = _defaultLocalPosition + _recoilPositionOffset;
            transform.localRotation = _defaultLocalRotation * Quaternion.Euler(_recoilRotationOffset);
        }

        private void CrossFade(string stateName, float transitionDuration)
        {
            if (animator == null || string.IsNullOrEmpty(stateName))
            {
                return;
            }

            animator.CrossFade(Animator.StringToHash(stateName), transitionDuration);
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            foreach (Transform child in root)
            {
                if (child.name == childName)
                {
                    return child;
                }

                Transform foundChild = FindChildRecursive(child, childName);
                if (foundChild != null)
                {
                    return foundChild;
                }
            }

            return null;
        }
    }
}
