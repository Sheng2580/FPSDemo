using System.Collections;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine;
using UnityEngine.Rendering;
using Weapon.Data;

namespace Weapon
{
    public class WeaponView : MonoBehaviour
    {
        private const float DefaultAnimatorSpeed = 1f;
        private const float MinReloadAnimationSpeed = 0.1f;
        private const float MaxReloadAnimationSpeed = 5f;
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
        [SerializeField] private WeaponSkillAnimationLibrary skillAnimationLibrary;
        [SerializeField] private string skillAnimationLibraryResourcePath = "SkillAnimationConfigs/DefaultWeaponSkillAnimationLibrary";
        [SerializeField] private float skillAnimationFadeOutTime = 0.08f;

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
        private Vector3 _skillPositionOffset;
        private Vector3 _skillRotationOffset;
        private float _adsAmount;
        private float _lastAdsAmount;
        private bool _hasCachedDefaultTransform;
        private PlayableGraph _skillAnimationGraph;
        private AnimationMixerPlayable _skillAnimationMixer;
        private Coroutine _skillAnimationRoutine;
        private Coroutine _proceduralSkillRoutine;

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

        private void OnDisable()
        {
            StopSkillAnimationPlayback(true);
            StopProceduralSkillAnimation();
        }

        public void Init(WeaponConfig config)
        {
            _config = config;
            CacheReferences();
            CacheDefaultTransform(false);
            ApplyViewModelShadowSettings();
        }

        public void PlayIdle(float transitionDuration = 0f)
        {
            ResetAnimationSpeed();
            CrossFade(_config?.idleStateName, transitionDuration);
        }

        public void PlayEquip()
        {
            ResetAnimationSpeed();
            CrossFade(_config?.equipStateName, _config != null ? _config.equipTransition : 0f);
        }

        public void PlayFire()
        {
            ResetAnimationSpeed();
            CrossFade(_config?.fireStateName, _config != null ? _config.fireTransition : 0f);
            ApplyViewRecoil();
        }

        public void PlayReload(float reloadDuration)
        {
            ApplyReloadAnimationSpeed(reloadDuration);
            CrossFade(_config?.reloadStateName, _config != null ? _config.reloadTransition : 0f);
        }

        public bool TryPlaySkillAnimation(string stateName, float skillDuration, float transitionDuration = 0.05f)
        {
            if (animator == null || string.IsNullOrEmpty(stateName))
            {
                return false;
            }

            // 技能片段优先走独立 Playable 避免枪械控制器立刻切回 Idle
            AnimationClip skillClip = ResolveSkillAnimationClip(stateName);
            if (skillClip != null)
            {
                PlaySkillClip(skillClip, skillDuration, transitionDuration);
                return true;
            }

            return TryPlayAnimatorSkillState(stateName, skillDuration, transitionDuration);
        }

        public void PlayPushViewAnimation(float duration, float sideSign)
        {
            StopProceduralSkillAnimation();
            StopSkillAnimationPlayback(true);
            _proceduralSkillRoutine = StartCoroutine(PushViewAnimationRoutine(duration, sideSign));
        }

        private bool TryPlayAnimatorSkillState(string stateName, float skillDuration, float transitionDuration)
        {
            int stateHash = Animator.StringToHash(stateName);
            if (!animator.HasState(0, stateHash))
            {
                return false;
            }

            ApplySkillAnimationSpeed(stateName, skillDuration);
            animator.CrossFade(stateHash, Mathf.Max(0f, transitionDuration));
            return true;
        }

        public void ResetAnimationSpeed()
        {
            if (animator == null)
            {
                return;
            }

            animator.speed = DefaultAnimatorSpeed;
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
            StopSkillAnimationPlayback(true);
            StopProceduralSkillAnimation();

            _adsAmount = 0f;
            _lastAdsAmount = 0f;
            _smoothPositionVelocity = Vector3.zero;
            _smoothRotationVelocity = Vector3.zero;
            _smoothScaleVelocity = Vector3.zero;
            _recoilPositionOffset = Vector3.zero;
            _recoilRotationOffset = Vector3.zero;
            _recoilPositionVelocity = Vector3.zero;
            _recoilRotationVelocity = Vector3.zero;
            _skillPositionOffset = Vector3.zero;
            _skillRotationOffset = Vector3.zero;
            _smoothedViewLocalPosition = _defaultViewLocalPosition;
            _smoothedViewLocalEulerAngles = _defaultViewLocalRotation.eulerAngles;
            _smoothedViewLocalScale = _defaultViewLocalScale;

            Transform root = GetViewRoot();
            root.localPosition = _defaultViewLocalPosition;
            root.localRotation = _defaultViewLocalRotation;
            root.localScale = _defaultViewLocalScale;

            if (animator != null)
            {
                ResetAnimationSpeed();
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

            root.localPosition = _smoothedViewLocalPosition + _recoilPositionOffset + _skillPositionOffset;
            root.localRotation = Quaternion.Euler(_smoothedViewLocalEulerAngles)
                                 * Quaternion.Euler(_recoilRotationOffset + _skillRotationOffset);
            root.localScale = _smoothedViewLocalScale;
            _lastAdsAmount = _adsAmount;
        }

        private IEnumerator PushViewAnimationRoutine(float duration, float sideSign)
        {
            duration = Mathf.Max(0.2f, duration);
            sideSign = sideSign < 0f ? -1f : 1f;

            Vector3 windupPosition = new Vector3(0.045f * sideSign, -0.05f, -0.18f);
            Vector3 windupRotation = new Vector3(12f, 7f * sideSign, -5f * sideSign);
            Vector3 strikePosition = new Vector3(-0.035f * sideSign, 0.018f, 0.24f);
            Vector3 strikeRotation = new Vector3(-16f, -9f * sideSign, 4f * sideSign);

            yield return AnimateSkillPose(Vector3.zero, windupPosition, Vector3.zero, windupRotation, duration * 0.24f);
            yield return AnimateSkillPose(windupPosition, strikePosition, windupRotation, strikeRotation, duration * 0.2f);

            float holdTime = duration * 0.08f;
            float holdTimer = 0f;
            while (holdTimer < holdTime)
            {
                holdTimer += Time.deltaTime;
                _skillPositionOffset = strikePosition;
                _skillRotationOffset = strikeRotation;
                yield return null;
            }

            yield return AnimateSkillPose(strikePosition, Vector3.zero, strikeRotation, Vector3.zero, duration * 0.48f);
            _skillPositionOffset = Vector3.zero;
            _skillRotationOffset = Vector3.zero;
            _proceduralSkillRoutine = null;
        }

        private IEnumerator AnimateSkillPose(
            Vector3 fromPosition,
            Vector3 toPosition,
            Vector3 fromRotation,
            Vector3 toRotation,
            float duration)
        {
            duration = Mathf.Max(0.01f, duration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);
                _skillPositionOffset = Vector3.LerpUnclamped(fromPosition, toPosition, t);
                _skillRotationOffset = Vector3.LerpUnclamped(fromRotation, toRotation, t);
                yield return null;
            }

            _skillPositionOffset = toPosition;
            _skillRotationOffset = toRotation;
        }

        private void StopProceduralSkillAnimation()
        {
            if (_proceduralSkillRoutine != null)
            {
                StopCoroutine(_proceduralSkillRoutine);
                _proceduralSkillRoutine = null;
            }

            _skillPositionOffset = Vector3.zero;
            _skillRotationOffset = Vector3.zero;
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

        private void ApplyReloadAnimationSpeed(float reloadDuration)
        {
            if (animator == null || _config == null || reloadDuration <= 0f)
            {
                ResetAnimationSpeed();
                return;
            }

            // reloadTime 同时控制逻辑换弹时间和动画播放速度 保证换弹表现不会被硬切
            float clipLength = GetAnimationClipLength(_config.reloadStateName, reloadDuration);
            float speed = clipLength / reloadDuration;
            animator.speed = Mathf.Clamp(speed, MinReloadAnimationSpeed, MaxReloadAnimationSpeed);
        }

        private void ApplySkillAnimationSpeed(string stateName, float skillDuration)
        {
            if (animator == null || skillDuration <= 0f)
            {
                ResetAnimationSpeed();
                return;
            }

            // 技能动画速度跟随技能持续时间 避免技能判定和手部动作脱节
            float clipLength = GetAnimationClipLength(stateName, skillDuration);
            float speed = clipLength / skillDuration;
            animator.speed = Mathf.Clamp(speed, MinReloadAnimationSpeed, MaxReloadAnimationSpeed);
        }

        private AnimationClip ResolveSkillAnimationClip(string stateName)
        {
            WeaponSkillAnimationLibrary library = ResolveSkillAnimationLibrary();
            if (library == null)
            {
                return null;
            }

            return library.TryGetClip(stateName, out AnimationClip clip) ? clip : null;
        }

        private WeaponSkillAnimationLibrary ResolveSkillAnimationLibrary()
        {
            if (skillAnimationLibrary == null && !string.IsNullOrEmpty(skillAnimationLibraryResourcePath))
            {
                skillAnimationLibrary = Resources.Load<WeaponSkillAnimationLibrary>(skillAnimationLibraryResourcePath);
            }

            return skillAnimationLibrary;
        }

        private void PlaySkillClip(AnimationClip clip, float skillDuration, float fadeInTime)
        {
            StopSkillAnimationPlayback(true);
            ResetAnimationSpeed();
            _skillAnimationRoutine = StartCoroutine(SkillClipRoutine(clip, skillDuration, fadeInTime));
        }

        private IEnumerator SkillClipRoutine(AnimationClip clip, float skillDuration, float fadeInTime)
        {
            if (animator == null || clip == null)
            {
                _skillAnimationRoutine = null;
                yield break;
            }

            float duration = skillDuration > 0f ? skillDuration : clip.length;
            float safeFadeIn = Mathf.Clamp(fadeInTime, 0f, duration * 0.45f);
            float safeFadeOut = Mathf.Clamp(skillAnimationFadeOutTime, 0f, duration * 0.45f);
            RuntimeAnimatorController controller = animator.runtimeAnimatorController;

            _skillAnimationGraph = PlayableGraph.Create($"{name} Skill Animation");
            _skillAnimationGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            AnimationPlayableOutput output = AnimationPlayableOutput.Create(_skillAnimationGraph, "Skill Animation", animator);
            AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(_skillAnimationGraph, clip);
            clipPlayable.SetTime(0f);
            clipPlayable.SetApplyFootIK(false);
            clipPlayable.SetApplyPlayableIK(false);
            float playbackSpeed = clip.length > 0f
                ? clip.length / Mathf.Max(0.01f, duration)
                : 1f;
            clipPlayable.SetSpeed(Mathf.Clamp(playbackSpeed, MinReloadAnimationSpeed, MaxReloadAnimationSpeed));

            bool useMixer = controller != null;
            if (useMixer)
            {
                AnimatorControllerPlayable controllerPlayable = AnimatorControllerPlayable.Create(_skillAnimationGraph, controller);
                _skillAnimationMixer = AnimationMixerPlayable.Create(_skillAnimationGraph, 2);
                _skillAnimationGraph.Connect(controllerPlayable, 0, _skillAnimationMixer, 0);
                _skillAnimationGraph.Connect(clipPlayable, 0, _skillAnimationMixer, 1);
                SetSkillPlayableWeight(0f);
                output.SetSourcePlayable(_skillAnimationMixer);
            }
            else
            {
                output.SetSourcePlayable(clipPlayable);
            }

            _skillAnimationGraph.Play();

            if (useMixer && safeFadeIn > 0f)
            {
                yield return FadeSkillPlayableWeight(0f, 1f, safeFadeIn);
            }
            else
            {
                SetSkillPlayableWeight(1f);
            }

            float holdTime = Mathf.Max(0.01f, duration - safeFadeIn - safeFadeOut);
            float elapsed = 0f;
            while (elapsed < holdTime && _skillAnimationGraph.IsValid())
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (useMixer && safeFadeOut > 0f)
            {
                yield return FadeSkillPlayableWeight(1f, 0f, safeFadeOut);
            }

            StopSkillAnimationPlayback(false);
            PlayIdle(skillAnimationFadeOutTime);
        }

        private IEnumerator FadeSkillPlayableWeight(float from, float to, float duration)
        {
            duration = Mathf.Max(0.001f, duration);
            float elapsed = 0f;

            while (elapsed < duration && _skillAnimationMixer.IsValid())
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smoothT = t * t * (3f - 2f * t);
                SetSkillPlayableWeight(Mathf.Lerp(from, to, smoothT));
                yield return null;
            }

            SetSkillPlayableWeight(to);
        }

        private void SetSkillPlayableWeight(float clipWeight)
        {
            if (!_skillAnimationMixer.IsValid())
            {
                return;
            }

            clipWeight = Mathf.Clamp01(clipWeight);
            _skillAnimationMixer.SetInputWeight(0, 1f - clipWeight);
            _skillAnimationMixer.SetInputWeight(1, clipWeight);
        }

        private void StopSkillAnimationPlayback(bool stopCoroutine)
        {
            if (stopCoroutine && _skillAnimationRoutine != null)
            {
                StopCoroutine(_skillAnimationRoutine);
            }

            _skillAnimationRoutine = null;

            if (_skillAnimationGraph.IsValid())
            {
                _skillAnimationGraph.Destroy();
            }

            _skillAnimationMixer = default;
        }

        private float GetAnimationClipLength(string stateName, float fallbackLength)
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
                    return Mathf.Max(0.01f, clip.length);
                }
            }

            return fallbackLength;
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
