using System;
using UnityEngine;

namespace SqueakSqueak.Cleaning
{
    [DisallowMultipleComponent]
    public sealed class ObjectDirt : MonoBehaviour
    {
        [SerializeField, Min(0.01f)]
        [Tooltip("이 오염을 제거하는 데 필요한 내구도입니다.")]
        private float durability = 1f;

        [SerializeField, Range(0.05f, 1f)]
        [Tooltip("제거 직전까지 남겨 둘 최소 크기 비율입니다.")]
        private float minimumScaleRatio = 0.25f;

        private Vector3 initialLocalScale;

        public event Action<ObjectDirt> Removed;

        public float CurrentDurability { get; private set; }

        public bool IsRemoved { get; private set; }

        /// <summary>생성한 단계 오염의 내구도/원래 크기를 활성화 전에 확정합니다.</summary>
        public void Configure(float initialDurability, Vector3 localScale)
        {
            if (!float.IsFinite(initialDurability) || initialDurability < .01f)
                throw new ArgumentOutOfRangeException(nameof(initialDurability));
            durability = initialDurability;
            initialLocalScale = localScale;
            transform.localScale = localScale;
            CurrentDurability = durability;
            IsRemoved = false;
        }

        private void Awake()
        {
            initialLocalScale = transform.localScale;
            ResetDirt();
        }

        public bool ApplyCleaning(float amount)
        {
            if (IsRemoved || amount <= 0f)
            {
                return false;
            }

            CurrentDurability = Mathf.Max(0f, CurrentDurability - amount);
            var remainingRatio = CurrentDurability / durability;
            transform.localScale = initialLocalScale * Mathf.Lerp(minimumScaleRatio, 1f, remainingRatio);

            if (CurrentDurability > 0f)
            {
                return true;
            }

            IsRemoved = true;
            Removed?.Invoke(this);
            gameObject.SetActive(false);
            return true;
        }

        public void ResetDirt()
        {
            CurrentDurability = durability;
            IsRemoved = false;
            transform.localScale = initialLocalScale;
            gameObject.SetActive(true);
        }

        private void OnValidate()
        {
            durability = Mathf.Max(0.01f, durability);
            minimumScaleRatio = Mathf.Clamp(minimumScaleRatio, 0.05f, 1f);
        }
    }
}
