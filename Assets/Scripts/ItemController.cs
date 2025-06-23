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

        private void StartEffect(
            ItemType itemType,
            GameObject sliderPrefab,
            float duration,
            Color color,
            Action onStart,
            Action onEnd)
        {
            // Nếu đã có hiệu ứng này đang chạy, dừng lại
            if (activeEffectCoroutine.TryGetValue(itemType, out Coroutine existingCoroutine))
            {
                StopCoroutine(existingCoroutine);
            }

            // Gọi bắt đầu hiệu ứng
            onStart?.Invoke();

            // Bắt đầu hiệu ứng mới
            Coroutine newCoroutine = StartCoroutine(EffectCoroutine(itemType, duration, onEnd));
            activeEffectCoroutine[itemType] = newCoroutine;

            // Gọi UI countdown
            ItemCountDown.Instance.ShowEffectCountdown(itemType, sliderPrefab, duration, color);
        }

        private IEnumerator EffectCoroutine(ItemType itemType, float duration, Action onEnd)
        {
            yield return new WaitForSeconds(duration);
            onEnd?.Invoke();
            activeEffectCoroutine.Remove(itemType);
        }

        public void StartBombPlusTemporary()
        {
            StartEffect(
                ItemType.BombPlus,
                ItemCountDown.Instance.bombPlusSlider,
                10f,
                Color.red,
                () => {
                    if (BombController.Instance.bombsRemaining < 2)
                    {
                        BombController.Instance.bombsRemaining++;
                        Debug.Log($"Bomb explosion, Bombs remaining: {BombController.Instance.bombsRemaining}");
                    }
                    Debug.Log("BombPlus +1");
                },
                () => {
                    //if (BombController.Instance.bombsRemaining >= 2)
                    //{
                        BombController.Instance.bombsRemaining--;
                    //}
                    Debug.Log("BombPlus -1");
                }
            );
        }

        public void StartBombExtraRangeTemporary()
        {
            StartEffect(
                ItemType.BombExtraRange,
                ItemCountDown.Instance.bombExtraRangeSlider,
                10f,
                Color.yellow,
                () => {
                    BombController.Instance.currentExplosionPrefab = BombController.Instance.explosionExtraRangePrefab;
                },
                () => {
                    BombController.Instance.currentExplosionPrefab = BombController.Instance.explosionDefaultPrefab;
                }
            );
        }

        public void StartSpeedUpTemporary()
        {
            StartEffect(
                ItemType.SpeedUp,
                ItemCountDown.Instance.speedUpSlider,
                10f,
                Color.blue,
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
