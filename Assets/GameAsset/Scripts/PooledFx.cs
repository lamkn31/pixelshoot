using System.Collections;
using UnityEngine;

namespace Wayfu.Lamkn
{
    // Gắn tự động lên mỗi instance FX trong pool (FxController.AddComponent nếu prefab chưa có).
    // Phát particle khi được lấy ra, rồi tự hẹn trả về pool khi FX chạy xong.
    [DisallowMultipleComponent]
    public sealed class PooledFx : MonoBehaviour
    {
        // Trả về pool sau khoảng này nếu FX không có ParticleSystem để đo (giây).
        private const float NoParticleLifetime = 1f;

        [SerializeField]
        [Tooltip("Lệch vị trí (theo hướng phát) so với điểm phát — riêng cho FX này.")]
        private Vector3 _offset;

        [SerializeField]
        [Tooltip("Lệch GÓC XOAY (euler) cộng thêm cho FX khi sinh — riêng cho FX này (vd để aura/khói quay đúng hướng).")]
        private Vector3 _eulerOffset;

        // Prefab gốc — FxController dùng để trả về đúng hàng đợi.
        public GameObject SourcePrefab { get; set; }

        private ParticleSystem[] _particles;
        private FxController _owner;
        private Coroutine _returnCo;

        private void Awake()
        {
            _particles = GetComponentsInChildren<ParticleSystem>(true);
        }

        // Đặt vị trí (cộng offset riêng) rồi phát; trả về pool theo đúng thời gian sống của FX (particle tự tắt hết).
        public void Play(FxController owner, Vector3 position, Quaternion rotation)
        {
            _owner = owner;
            transform.SetParent(owner.transform, false); // tách khỏi parent cũ (nếu có) khi phát rời
            transform.SetPositionAndRotation(position + rotation * _offset, rotation * Quaternion.Euler(_eulerOffset));
            StopReturnCo();

            if (PlayParticles())
                _returnCo = StartCoroutine(ReturnWhenParticlesDone());
            else
                _returnCo = StartCoroutine(ReturnAfter(NoParticleLifetime));
        }

        // Gắn làm con của 'parent' (offset = localPosition) và phát loop; KHÔNG tự về pool — gọi FxController.Return thủ công.
        public void PlayAttached(FxController owner, Transform parent)
        {
            _owner = owner;
            StopReturnCo();
            transform.SetParent(parent, false);
            transform.localRotation = Quaternion.Euler(_eulerOffset);
            transform.localPosition = _offset;
            PlayParticles();
        }

        // Clear + Play toàn bộ particle. Trả true nếu FX có particle.
        private bool PlayParticles()
        {
            if (_particles == null || _particles.Length == 0) return false;
            foreach (ParticleSystem ps in _particles)
            {
                ps.Clear(true);
                ps.Play(true);
            }
            return true;
        }

        private void StopReturnCo()
        {
            if (_returnCo != null) { StopCoroutine(_returnCo); _returnCo = null; }
        }

        private IEnumerator ReturnWhenParticlesDone()
        {
            // Chờ tới khi không còn particle nào sống (particle non-loop sẽ tự hết theo thời lượng của chính nó).
            bool alive = true;
            while (alive)
            {
                yield return null;
                alive = false;
                foreach (ParticleSystem ps in _particles)
                {
                    if (ps.IsAlive(true)) { alive = true; break; }
                }
            }
            ReturnNow();
        }

        private IEnumerator ReturnAfter(float t)
        {
            yield return new WaitForSeconds(t);
            ReturnNow();
        }

        private void ReturnNow()
        {
            _returnCo = null;
            if (_owner != null) _owner.Return(this);
        }
    }
}
