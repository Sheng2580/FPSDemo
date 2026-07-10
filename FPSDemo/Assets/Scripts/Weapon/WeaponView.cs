using UnityEngine;
using UnityEngine.Rendering;
using Weapon.Data;

namespace Weapon
{
    public class WeaponView : MonoBehaviour
    {
        private static readonly int AmmoHash = Animator.StringToHash("Ammo");
        private static readonly int IsReloadingHash = Animator.StringToHash("Is Reloading");
        private static readonly int ADSAmountHash = Animator.StringToHash("ADS Amount");
        private static readonly int SprintAmountHash = Animator.StringToHash("Sprint Amount");
        private static readonly Vector3 DefaultAimViewPositionOffset = new Vector3(-0.06f, 0.044f, 0.02f);

        [SerializeField] private Animator animator;
        [SerializeField] private Transform viewRoot;
        [SerializeField] private Transform muzzlePoint;
        [SerializeField] private Transform shellPoint;
        [SerializeField] private bool disableViewModelShadows = true;

        private WeaponConfig _config;
        private Vector3 _defaultViewLocalPosition;
        private Quaternion _defaultViewLocalRotation;
        private Vector3 _defaultViewLocalScale;
        private Vector3 _smoothedViewLocalPosition;
        private Vector3 _smoothedViewLocalEulerAngles;
        private Vector3 _smoothedViewLocalScale;
        private Vector3 _smoothPositionVelocity;
        private Vector3 _smoothRotationVelocity;
        private Vector3 _smoothScaleVelocity;
        private Vector3 _recoilPositionOffset;
        private Vector3 _recoilRotationOffset;
        private Vector3 _recoilPositionVelocity;
        private Vector3 _recoilRotationVelocity;
        private float _adsAmount;
        private float _lastAdsAmount;
        private bool _hasCachedDefaultTransform;

        public Animator Animator => animator;
        public Transform MuzzlePoint => muzzlePoint;
        public Transform ShellPoint => shellPoint;

        private void Awake()
        {
            CacheReferences();
            CacheDefaultTransform(true);
            ApplyViewModelShadowSettings();
        }

        private void Reset()
        {
            CacheReferences();
            CacheDefaultTransform(true);
        }

        private void LateUpdate()
        {
            UpdateViewPose();
        }

        public void Init(WeaponConfig config)
        {
            _config = config;
            CacheReferences();
            CacheDefaultTransform(false);
            ApplyViewModelShadowSettings();
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

        public float GetAnimationLength(string stateName, float fallbackLength)
        {
            if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(stateName))
            {
                return fallbackLength;
            }

            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip == null)
                {
                    continue;
                }

                if (clip.name == stateName || clip.name.Contains(stateName))
                {
                    return Mathf.Max(clip.length, fallbackLength);
                }
            }

            return fallbackLength;
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
            _adsAmount = Mathf.Clamp01(value);

            if (animator == null)
            {
                return;
            }

            animator.SetFloat(ADSAmountHash, _adsAmount);
        }

        public void SetSprintAmount(float value)
        {
            if (animator == null)
            {
                return;
            }

            animator.SetFloat(SprintAmountHash, value);
        }

        public void ResetPoseInstant()
        {
            CacheReferences();
            CacheDefaultTransform(false);

            _adsAmount = 0f;
            _lastAdsAmount = 0f;
            _smoothPositionVelocity = Vector3.zero;
            _smoothRotationVelocity = Vector3.zero;
            _smoothScaleVelocity = Vector3.zero;
            _recoilPositionOffset = Vector3.zero;
            _recoilRotationOffset = Vector3.zero;
            _recoilPositionVelocity = Vector3.zero;
            _recoilRotationVelocity = Vector3.zero;
            _smoothedViewLocalPosition = _defaultViewLocalPosition;
            _smoothedViewLocalEulerAngles = _defaultViewLocalRotation.eulerAngles;
            _smoothedViewLocalScale = _defaultViewLocalScale;

            Transform root = GetViewRoot();
            root.localPosition = _defaultViewLocalPosition;
            root.localRotation = _defaultViewLocalRotation;
            root.localScale = _defaultViewLocalScale;

            if (animator != null)
            {
                animator.SetFloat(ADSAmountHash, 0f);
                animator.SetFloat(SprintAmountHash, 0f);
                animator.SetBool(IsReloadingHash, false);
            }
        }

        private void CacheReferences()
        {
            animator ??= GetComponent<Animator>();
            animator ??= GetComponentInChildren<Animator>(true);
            viewRoot ??= animator != null ? animator.transform : transform;
            muzzlePoint ??= FindChildRecursive(transform, "MuzzlePoint");
            shellPoint ??= FindChildRecursive(transform, "ShellPoint");
        }

        private void ApplyViewModelShadowSettings()
        {
            if (!disableViewModelShadows)
            {
                return;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer viewRenderer = renderers[i];
                if (viewRenderer == null)
                {
                    continue;
                }

                // 第一人称手臂和枪不接收也不投射场景阴影 避免手机端近距离阴影贴图块状放大
                viewRenderer.shadowCastingMode = ShadowCastingMode.Off;
                viewRenderer.receiveShadows = false;
            }
        }

        private void CacheDefaultTransform(bool force)
        {
            if (_hasCachedDefaultTransform && !force)
            {
                return;
            }

            Transform root = GetViewRoot();
            _defaultViewLocalPosition = root.localPosition;
            _defaultViewLocalRotation = root.localRotation;
            _defaultViewLocalScale = root.localScale;
            _smoothedViewLocalPosition = _defaultViewLocalPosition;
            _smoothedViewLocalEulerAngles = _defaultViewLocalRotation.eulerAngles;
            _smoothedViewLocalScale = _defaultViewLocalScale;
            _smoothPositionVelocity = Vector3.zero;
            _smoothRotationVelocity = Vector3.zero;
            _smoothScaleVelocity = Vector3.zero;
            _hasCachedDefaultTransform = true;
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

        private void UpdateViewPose()
        {
            Transform root = GetViewRoot();
            float returnSpeed = _config != null ? _config.viewRecoilReturnSpeed : 18f;
            float recoilSmoothTime = 1f / Mathf.Max(1f, returnSpeed);
            float deltaTime = Time.deltaTime;

            // 后坐力回位使用 SmoothDamp 避免枪模被硬拉回默认姿态
            _recoilPositionOffset = Vector3.SmoothDamp(
                _recoilPositionOffset,
                Vector3.zero,
                ref _recoilPositionVelocity,
                recoilSmoothTime,
                Mathf.Infinity,
                deltaTime);

            _recoilRotationOffset = new Vector3(
                Mathf.SmoothDampAngle(_recoilRotationOffset.x, 0f, ref _recoilRotationVelocity.x, recoilSmoothTime, Mathf.Infinity, deltaTime),
                Mathf.SmoothDampAngle(_recoilRotationOffset.y, 0f, ref _recoilRotationVelocity.y, recoilSmoothTime, Mathf.Infinity, deltaTime),
                Mathf.SmoothDampAngle(_recoilRotationOffset.z, 0f, ref _recoilRotationVelocity.z, recoilSmoothTime, Mathf.Infinity, deltaTime));

            GetViewPose(out Vector3 viewPosition, out Quaternion viewRotation, out Vector3 viewScale);
            SmoothD(viewPosition, viewRotation.eulerAngles, viewScale);

            root.localPosition = _smoothedViewLocalPosition + _recoilPositionOffset;
            root.localRotation = Quaternion.Euler(_smoothedViewLocalEulerAngles) * Quaternion.Euler(_recoilRotationOffset);
            root.localScale = _smoothedViewLocalScale;
            _lastAdsAmount = _adsAmount;
        }

        private Transform GetViewRoot()
        {
            return viewRoot != null ? viewRoot : transform;
        }

        private void GetViewPose(out Vector3 viewPosition, out Quaternion viewRotation, out Vector3 viewScale)
        {
            if (_config != null && _config.useAimLocalPose)
            {
                // 默认姿态运行时缓存 开镜姿态由每把武器的数据配置
                viewPosition = Vector3.Lerp(_defaultViewLocalPosition, _config.aimLocalPosition, _adsAmount);
                viewRotation = Quaternion.Slerp(_defaultViewLocalRotation, Quaternion.Euler(_config.aimLocalEulerAngles), _adsAmount);
                viewScale = Vector3.Lerp(_defaultViewLocalScale, GetAimLocalScale(), _adsAmount);
                return;
            }

            Vector3 aimPositionOffset = _config != null
                ? Vector3.Lerp(Vector3.zero, GetAimViewPositionOffset(), _adsAmount)
                : Vector3.zero;
            Vector3 aimRotationOffset = _config != null
                ? Vector3.Lerp(Vector3.zero, _config.aimViewRotationOffset, _adsAmount)
                : Vector3.zero;

            viewPosition = _defaultViewLocalPosition + aimPositionOffset;
            viewRotation = _defaultViewLocalRotation * Quaternion.Euler(aimRotationOffset);
            viewScale = _defaultViewLocalScale;
        }

        private void SmoothD(Vector3 targetPosition, Vector3 targetEulerAngles, Vector3 targetScale)
        {
            float smoothTime = GetAimPoseSmoothTime();

            _smoothedViewLocalPosition = Vector3.SmoothDamp(
                _smoothedViewLocalPosition,
                targetPosition,
                ref _smoothPositionVelocity,
                smoothTime);

            _smoothedViewLocalEulerAngles = new Vector3(
                Mathf.SmoothDampAngle(_smoothedViewLocalEulerAngles.x, targetEulerAngles.x, ref _smoothRotationVelocity.x, smoothTime),
                Mathf.SmoothDampAngle(_smoothedViewLocalEulerAngles.y, targetEulerAngles.y, ref _smoothRotationVelocity.y, smoothTime),
                Mathf.SmoothDampAngle(_smoothedViewLocalEulerAngles.z, targetEulerAngles.z, ref _smoothRotationVelocity.z, smoothTime));

            _smoothedViewLocalScale = Vector3.SmoothDamp(
                _smoothedViewLocalScale,
                targetScale,
                ref _smoothScaleVelocity,
                smoothTime);
        }

        private float GetAimPoseSmoothTime()
        {
            if (_config == null)
            {
                return 0.08f;
            }

            float aimSpeed = _adsAmount >= _lastAdsAmount ? _config.aimInSpeed : _config.aimOutSpeed;
            return 1f / Mathf.Max(1f, aimSpeed);
        }

        private Vector3 GetAimLocalScale()
        {
            if (_config == null || _config.aimLocalScale == Vector3.zero)
            {
                return _defaultViewLocalScale;
            }

            return _config.aimLocalScale;
        }

        private Vector3 GetAimViewPositionOffset()
        {
            if (_config == null || _config.aimViewPositionOffset == Vector3.zero)
            {
                return DefaultAimViewPositionOffset;
            }

            return _config.aimViewPositionOffset;
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
