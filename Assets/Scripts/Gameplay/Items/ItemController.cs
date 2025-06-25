using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts
{
    // Assign to Player GameObject in Unity
    public class ItemController : MonoBehaviour
    {
        public static ItemController Instance { get; private set; }

        private Dictionary<ItemType, Coroutine> activeEffectCoroutine = new Dictionary<ItemType, Coroutine>();

        [Header("Sliders")]
        [SerializeField]
        private Transform sliderWrapper;
        [SerializeField]
        private GameObject bombPlusSlider;
        [SerializeField]
        private GameObject bombExtraRangeSlider;
        [SerializeField]
        private GameObject speedUpSlider;

        private void Start()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        public void StartCoroutine(Coroutine routine)
        {
            if (routine == null)
            {
                StartCoroutine(routine);
            }
        }

        private void StartEffect(ItemType itemType, GameObject sliderPrefab, float duration, Action onStart, Action onEnd)
        {
            // find and delete old slider if exists
            if (activeEffectCoroutine.TryGetValue(itemType, out Coroutine existingCoroutine))
            {
                StopCoroutine(existingCoroutine);
                activeEffectCoroutine.Remove(itemType);
                foreach (Transform child in sliderWrapper)
                {
                    Console.WriteLine(child.name);
                    if (child.name == sliderPrefab.name.ToString() + "(Clone)")
                    {
                        Destroy(child.gameObject);
                        break;
                    }
                }
            }

            onStart?.Invoke();

            Coroutine newCoroutine = StartCoroutine(EffectCoroutine(sliderPrefab, itemType, duration, onEnd));
            activeEffectCoroutine[itemType] = newCoroutine;
        }

        private IEnumerator EffectCoroutine(GameObject sliderPrefab, ItemType itemType, float duration, Action onEnd)
        {
            GameObject sliderGO = Instantiate(sliderPrefab, sliderWrapper);
            yield return new WaitForSeconds(duration);
            Destroy(sliderGO.gameObject);
            onEnd?.Invoke();
            activeEffectCoroutine.Remove(itemType);
        }

        public void StartBombPlusTemporary()
        {
            StartEffect(
                ItemType.BombPlus,
                bombPlusSlider,
                10f,
                () => {
                    if (BombController.Instance.bombsRemaining < 2)
                    {
                        BombController.Instance.bombsRemaining++;
                    }
                },
                () => BombController.Instance.bombsRemaining = 1
            );
        }

        public void StartBombExtraRangeTemporary()
        {
            StartEffect(
                ItemType.BombExtraRange,
                bombExtraRangeSlider,
                10f,
                () => BombController.Instance.currentExplosionPrefab = BombController.Instance.explosionExtraRangePrefab,
                () => BombController.Instance.currentExplosionPrefab = BombController.Instance.explosionDefaultPrefab
            );
        }

        public void StartSpeedUpTemporary()
        {
            StartEffect(
                ItemType.SpeedUp,
                speedUpSlider,
                10f,
                () => {
                    MovementController.Instance.speed = Mathf.Min(
                        MovementController.Instance.speed + 1f,
                        MovementController.Instance.maxSpeed
                    );
                },
                () => {
                    MovementController.Instance.speed = MovementController.Instance.minSpeed;
                }
            );
        }
    }
}
