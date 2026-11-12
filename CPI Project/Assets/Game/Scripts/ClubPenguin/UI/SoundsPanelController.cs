using ClubPenguin.Audio;
using Disney.MobileNetwork;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ClubPenguin.UI
{
    public class SoundsPanelController : MonoBehaviour
    {
        public Slider MusicVolumeSlider;

        public Slider SoundsVolumeSlider;

        public Toggle ResetVolumeButton;

        public Toggle MuteButton;

        [Header("Volume Buttons")]
        public Toggle Volume1Button;
        public Toggle Volume2Button;
        public Toggle Volume3Button;
        public Toggle VolumeMaxButton;

        private GameSettings gameSettings;

        private float previousMusicVolume = 2f;
        private float previousSfxVolume = 2f;

        private bool isMuted;

        private void Start()
        {
            gameSettings = Service.Get<GameSettings>();

            MusicVolumeSlider.SetValueWithoutNotify(gameSettings.MusicVolume);
            SoundsVolumeSlider.SetValueWithoutNotify(gameSettings.SfxVolume);

            previousMusicVolume = MusicVolumeSlider.value;
            previousSfxVolume = SoundsVolumeSlider.value;

            isMuted = false;

            if (MuteButton != null)
            {
                MuteButton.SetIsOnWithoutNotify(false);
            }
        }

        private void OnEnable()
        {
            MusicVolumeSlider.onValueChanged.AddListener(onMusicVolumeChanged);
            SoundsVolumeSlider.onValueChanged.AddListener(onSfxVolumeChanged);

            if (ResetVolumeButton != null)
            {
                ResetVolumeButton.onValueChanged.AddListener(onResetVolumeButtonClicked);
            }

            if (MuteButton != null)
            {
                MuteButton.onValueChanged.AddListener(onMuteButtonClicked);
            }

            if (Volume1Button != null) Volume1Button.onValueChanged.AddListener(onVolume1Toggled);
            if (Volume2Button != null) Volume2Button.onValueChanged.AddListener(onVolume2Toggled);
            if (Volume3Button != null) Volume3Button.onValueChanged.AddListener(onVolume3Toggled);
            if (VolumeMaxButton != null) VolumeMaxButton.onValueChanged.AddListener(onVolumeMaxToggled);
        }

        private void OnDisable()
        {
            MusicVolumeSlider.onValueChanged.RemoveListener(onMusicVolumeChanged);
            SoundsVolumeSlider.onValueChanged.RemoveListener(onSfxVolumeChanged);

            if (ResetVolumeButton != null)
            {
                ResetVolumeButton.onValueChanged.RemoveListener(onResetVolumeButtonClicked);
            }

            if (MuteButton != null)
            {
                MuteButton.onValueChanged.RemoveListener(onMuteButtonClicked);
            }

            if (Volume1Button != null) Volume1Button.onValueChanged.RemoveListener(onVolume1Toggled);
            if (Volume2Button != null) Volume2Button.onValueChanged.RemoveListener(onVolume2Toggled);
            if (Volume3Button != null) Volume3Button.onValueChanged.RemoveListener(onVolume3Toggled);
            if (VolumeMaxButton != null) VolumeMaxButton.onValueChanged.RemoveListener(onVolumeMaxToggled);
        }

        private void onMusicVolumeChanged(float value)
        {
            previousMusicVolume = value;
            gameSettings.MusicVolume.SetValue(value);
            if (!isMuted)
            {
                Service.Get<AudioController>().SetMusicVolume(value);
            }
        }

        private void onSfxVolumeChanged(float value)
        {
            previousSfxVolume = value;
            gameSettings.SfxVolume.SetValue(value);
            if (!isMuted)
            {
                Service.Get<AudioController>().SetSFXVolume(value);
            }
        }

        public void SetMusicVolume(float value)
        {
            MusicVolumeSlider.value = Mathf.Clamp(value, MusicVolumeSlider.minValue, MusicVolumeSlider.maxValue);
        }

        public void SetSfxVolume(float value)
        {
            SoundsVolumeSlider.value = Mathf.Clamp(value, SoundsVolumeSlider.minValue, SoundsVolumeSlider.maxValue);
        }

        private void setVolume(float value)
        {
            unmuteForVolumePreset();
            SetMusicVolume(value);
            SetSfxVolume(value);
            clearVolumePresetToggles();
        }

        private void setVolumeToMax()
        {
            unmuteForVolumePreset();
            SetMusicVolume(MusicVolumeSlider.maxValue);
            SetSfxVolume(SoundsVolumeSlider.maxValue);
            clearVolumePresetToggles();
        }

        private void clearVolumePresetToggles()
        {
            resetVolumePresetToggle(Volume1Button);
            resetVolumePresetToggle(Volume2Button);
            resetVolumePresetToggle(Volume3Button);
            resetVolumePresetToggle(VolumeMaxButton);
        }

        private void resetVolumePresetToggle(Toggle toggle)
        {
            if (toggle == null)
            {
                return;
            }

            if (toggle.group != null)
            {
                toggle.group.allowSwitchOff = true;
            }

            toggle.SetIsOnWithoutNotify(false);
            StartCoroutine(resetToggleAnimation(toggle));
        }

        private IEnumerator resetToggleAnimation(Toggle toggle)
        {
            yield return null;

            if (toggle == null)
            {
                yield break;
            }

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                toggle.OnPointerExit(new PointerEventData(eventSystem));
                if (eventSystem.currentSelectedGameObject == toggle.gameObject)
                {
                    eventSystem.SetSelectedGameObject(null);
                }
            }

            if (toggle.transition != Selectable.Transition.Animation || toggle.animator == null)
            {
                yield break;
            }

            AnimationTriggers triggers = toggle.animationTriggers;
            Animator animator = toggle.animator;
            animator.ResetTrigger(triggers.highlightedTrigger);
            animator.ResetTrigger(triggers.pressedTrigger);
            animator.ResetTrigger(triggers.selectedTrigger);
            animator.ResetTrigger(triggers.disabledTrigger);
            animator.SetTrigger(triggers.normalTrigger);
        }

        private void unmuteForVolumePreset()
        {
            if (!isMuted)
            {
                return;
            }

            isMuted = false;
            if (MuteButton != null)
            {
                MuteButton.SetIsOnWithoutNotify(false);
            }
        }

        private void onVolume1Toggled(bool isOn)
        {
            setVolume(1f);
        }

        private void onVolume2Toggled(bool isOn)
        {
            setVolume(2f);
        }

        private void onVolume3Toggled(bool isOn)
        {
            setVolume(3f);
        }

        private void onVolumeMaxToggled(bool isOn)
        {
            setVolumeToMax();
        }

        private void onResetVolumeButtonClicked(bool value)
        {
            const float defaultVolume = 2f;

            previousMusicVolume = defaultVolume;
            previousSfxVolume = defaultVolume;

            if (isMuted)
            {
                isMuted = false;

                if (MuteButton != null)
                {
                    MuteButton.SetIsOnWithoutNotify(false);
                }
            }

            MusicVolumeSlider.value = defaultVolume;
            SoundsVolumeSlider.value = defaultVolume;

            Service.Get<AudioController>().SetMusicVolume(defaultVolume);
            Service.Get<AudioController>().SetSFXVolume(defaultVolume);

            gameSettings.MusicVolume.SetValue(defaultVolume);
            gameSettings.SfxVolume.SetValue(defaultVolume);

            resetVolumePresetToggle(ResetVolumeButton);
        }

        private void onMuteButtonClicked(bool value)
        {
            isMuted = value;

            if (isMuted)
            {
                previousMusicVolume = MusicVolumeSlider.value;
                previousSfxVolume = SoundsVolumeSlider.value;

                MusicVolumeSlider.SetValueWithoutNotify(0f);
                SoundsVolumeSlider.SetValueWithoutNotify(0f);

                Service.Get<AudioController>().SetMusicVolume(0f);
                Service.Get<AudioController>().SetSFXVolume(0f);
            }
            else
            {
                MusicVolumeSlider.SetValueWithoutNotify(previousMusicVolume);
                SoundsVolumeSlider.SetValueWithoutNotify(previousSfxVolume);

                Service.Get<AudioController>().SetMusicVolume(previousMusicVolume);
                Service.Get<AudioController>().SetSFXVolume(previousSfxVolume);
            }

            StartCoroutine(resetToggleAnimation(MuteButton));
        }
    }
}