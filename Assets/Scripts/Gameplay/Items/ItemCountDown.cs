using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts
{
    public class ItemCountDown : MonoBehaviour
    {
        public static ItemCountDown Instance { get; private set; }

        [Header("Sliders")]
        public GameObject bombPlusSlider;
        public GameObject bombExtraRangeSlider;
        public GameObject speedUpSlider;

        private Dictionary<ItemType, Coroutine> activeSliders = new();

        [Header("Object contain sliders")]
        public Transform sliderWrapper;

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        public void ShowEffectCountdown(ItemType itemType, GameObject itemSlider, float duration, Color barColor)
        {
            if (activeSliders.TryGetValue(itemType, out Coroutine oldCoroutine))
            {
                StopCoroutine(oldCoroutine);
                foreach (Transform child in sliderWrapper)
                {
                    if (child.name == itemType.ToString())
                    {
                        Destroy(child.gameObject);
                        break;
                    }
                }
            }

            GameObject sliderGO = Instantiate(itemSlider, sliderWrapper);
            sliderGO.name = itemType.ToString();

            Slider slider = sliderGO.GetComponent<Slider>();
            slider.maxValue = duration;
            slider.value = duration;

            Image fill = slider.fillRect.GetComponent<Image>();
            fill.color = barColor;

            Coroutine coroutine = StartCoroutine(UpdateSlider(itemType, slider, duration));
            activeSliders[itemType] = coroutine;
        }

        private IEnumerator UpdateSlider(ItemType itemType, Slider slider, float duration)
        {
            float timeLeft = duration;
            while (timeLeft > 0f)
            {
                timeLeft -= Time.deltaTime;
                slider.value = timeLeft;
                yield return null;
            }

            Destroy(slider.gameObject);
            activeSliders.Remove(itemType);
        }
    }
}
