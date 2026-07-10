using System.Collections;
using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Gun: nằm trong slot → click để ra path → chạy loop và tự bắn cột ngoài cùng gần nhất
    /// cùng màu; hết đạn thì biến mất (yêu cầu #3, #7). Di chuyển trên path do PathManager điều khiển.
    /// </summary>
    public class Gun : MonoBehaviour
    {
        private enum GunState { InSlot, OnPath, Dead }

        public GunData Data { get; private set; }
        public BlockColor Color => Data.Color;
        public GunSlot Slot { get; private set; }

        /// <summary>Khoảng cách tích luỹ trên path (PathManager ghi/đọc).</summary>
        public float Distance;
        public bool IsOnPath => _state == GunState.OnPath;

        private GunState _state = GunState.InSlot;
        private float _fireInterval = 0.25f;
        private float _fireTimer;
        private Renderer _renderer;
        private TextMesh _label;
        private Coroutine _moveRoutine;

        public void Init(GunData data, float fireInterval)
        {
            Data = new GunData { Color = data.Color, CountBullet = data.CountBullet };
            _fireInterval = fireInterval;

            _renderer = GetComponentInChildren<Renderer>();
            if (_renderer != null) _renderer.material.color = BlockColorPalette.ToColor(Data.Color);

            EnsureLabel();
            UpdateLabel();
            _state = GunState.InSlot;
        }

        public void SetSlot(GunSlot s) => Slot = s;

        private void OnMouseDown()
        {
            if (_state == GunState.InSlot) SlotManager.Instance?.OnGunClicked(this);
        }

        public void OnDeployed()
        {
            _state = GunState.OnPath;
            Slot = null;
            transform.SetParent(null);
            _fireTimer = 0f;
        }

        private void Update()
        {
            if (_state != GunState.OnPath) return;

            _fireTimer -= Time.deltaTime;
            if (_fireTimer <= 0f)
            {
                var col = GridBlockManager.Instance?.FindTargetColumn(Data.Color, transform.position);
                if (col != null)
                {
                    Fire(col);
                    _fireTimer = _fireInterval;
                }
            }
        }

        private void Fire(BlockColumn col)
        {
            Data.CountBullet--;
            UpdateLabel();
            col.HitOnce();

            if (Data.CountBullet <= 0) { Die(); return; }
            GameController.Instance?.OnBoardChanged();
        }

        private void Die()
        {
            _state = GunState.Dead;
            PathManager.Instance?.RemoveGun(this);
            GameController.Instance?.OnBoardChanged();
            Destroy(gameObject);
        }

        public void MoveTo(Vector3 target, float duration)
        {
            if (_moveRoutine != null) StopCoroutine(_moveRoutine);
            if (!gameObject.activeInHierarchy || duration <= 0f) { transform.position = target; return; }
            _moveRoutine = StartCoroutine(MoveRoutine(target, duration));
        }

        private IEnumerator MoveRoutine(Vector3 target, float dur)
        {
            Vector3 start = transform.position;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(start, target, t / dur);
                yield return null;
            }
            transform.position = target;
            _moveRoutine = null;
        }

        private void EnsureLabel()
        {
            _label = GetComponentInChildren<TextMesh>();
            if (_label != null) return;

            var go = new GameObject("BulletLabel");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.6f);
            go.transform.localScale = Vector3.one * 0.15f;
            _label = go.AddComponent<TextMesh>();
            _label.anchor = TextAnchor.MiddleCenter;
            _label.alignment = TextAlignment.Center;
            _label.fontSize = 64;
            _label.color = UnityEngine.Color.black;
        }

        private void UpdateLabel()
        {
            if (_label != null) _label.text = Data.CountBullet.ToString();
        }
    }
}
