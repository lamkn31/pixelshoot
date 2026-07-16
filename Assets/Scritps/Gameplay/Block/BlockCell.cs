using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// 1 cell block (~ BlockCell của PixelShoot_2): chứa 1 stack block cùng màu (lấy từ Pooler).
    /// Mỗi ĐẠN tới trừ 1 block; hết block → báo GridBlockManager dồn các cell phía sau (yêu cầu #5, #9).
    /// Có <see cref="_pendingHits"/> để không bắn dư đạn khi nhiều gun cùng nhắm 1 cell.
    /// </summary>
    public class BlockCell : MonoBehaviour, IItemPool<BlockCell>
    {
        [Tooltip("Node cha để gắn các block của stack (child 'BlocksContainer' trong prefab).")]
        [SerializeField] private Transform blocksContainer;
        [Tooltip("Bật khi cell đang nằm ở Ô GỐC của Spawner (child 'BlocksSpawnerIndicator').")]
        [SerializeField] private GameObject spawnerIndicator;

        public TypeColor Color { get; private set; }
        public int BlockCol { get; private set; }
        public int Depth { get; private set; }

        /// <summary>Cập nhật index cột khi cell dồn lên ô khác (Arc cột lệch: index có thể đổi).</summary>
        public void SetColumn(int col) => BlockCol = col;

        /// <summary>
        /// Cell đang TRƯỢT tới ô của nó (vừa nhả ra, hoặc đang dồn hàng) → gun không được ngắm.
        /// MoveTo tự bật khi bắt đầu trượt và tắt khi tới nơi. Nhờ vậy gun chỉ ngắm cell đã ĐỨNG YÊN
        /// ở hàng 0: cell kế vừa dồn tới nếu cùng màu thì bị bắn tiếp, khác màu thì gun bỏ cả cột đó
        /// (cell khác màu chặn mọi cell phía sau).
        /// </summary>
        public bool PendingEntry { get; private set; }

        /// <summary>
        /// Số THẾ HỆ — tăng mỗi lần Build. Cell là item pooled: object bị tái dùng cho cell khác ngay
        /// trong cùng frame, nên reference không đủ để biết "target còn sống". Gun/Bullet lưu Generation
        /// lúc chốt target; lệch = object đã thành cell khác → bỏ target, không bám theo ra ô mới.
        /// </summary>
        public int Generation { get; private set; }

        private readonly List<Block> _blocks = new List<Block>();
        private GridBlockManager _manager;
        private Quaternion _indicatorRestLocalRot = Quaternion.identity; // pose gốc của mũi tên trong prefab
        private Coroutine _moveRoutine;
        private int _pendingHits;
        private float _stackSpacing;
        private Vector3 _blockScale = Vector3.one;
        private Pooler<BlockCell> _pool;

        public void OnInitializedInPool(Pooler<BlockCell> pool) => _pool = pool;

        private void Awake()
        {
            // Nhớ pose gốc TRƯỚC khi ShowSpawnerIndicator kịp ghi đè — sau đó localRotation không còn là
            // giá trị dựng trong prefab nữa.
            if (spawnerIndicator != null) _indicatorRestLocalRot = spawnerIndicator.transform.localRotation;
        }

        public int StackCount => _blocks.Count;
        /// <summary>Số block chưa bị đạn "đặt chỗ" (đạn đang bay) — gun chỉ bắn khi còn &gt; 0.</summary>
        public int Available => _blocks.Count - _pendingHits;
        public bool IsEmpty => _blocks.Count == 0;

        public void Build(BlockCellData data, float stackSpacing, Vector3 blockScale, GridBlockManager manager)
        {
            _manager = manager;
            _stackSpacing = stackSpacing;
            _blockScale = blockScale == Vector3.zero ? Vector3.one : blockScale;
            BlockCol = data.BlockCol;
            Depth = data.SpawnerDepth;
            _pendingHits = 0;
            Generation++;                    // object pool tái dùng → đây là 1 cell MỚI
            PendingEntry = false;            // reset cho item pooled; MoveTo tự bật khi cell trượt
            ReleaseBlocks();                 // item pooled tái dùng: dọn stack cũ trước
            ShowSpawnerIndicator(false);

            Fill(data.Color, Mathf.Max(1, data.BlockStackCt));
        }

        /// <summary>
        /// Bật/tắt dấu hiệu "ô gốc Spawner" (ô cố định nhả cell ẩn ra) và quay nó theo hướng nhả.
        /// <para>Chỉ xoay quanh trục Y (world), CHỒNG lên pose gốc dựng trong prefab. Gán thẳng
        /// rotation = Euler(0,dirAngle,0) sẽ xoá luôn góc nghiêng đã dựng sẵn (mũi tên nằm phẳng trên
        /// sàn nhờ X=90) → mũi tên dựng đứng, camera top-down nhìn gần như không thấy.</para>
        /// <para>Đặt ở WORLD chứ không ăn theo cell: cell đứng ở ô gốc có thể là cell dồn từ hàng sau
        /// lên, mang góc riêng của nó — mũi tên phải theo hướng của NGUỒN, không phải của cell.</para>
        /// </summary>
        public void ShowSpawnerIndicator(bool on, float dirAngle = 0f)
        {
            Debug.Log(dirAngle);
            if (spawnerIndicator == null) return;
            spawnerIndicator.SetActive(on);
            if (on)
                spawnerIndicator.transform.rotation =
                    Quaternion.AngleAxis(dirAngle, Vector3.up) * _indicatorRestLocalRot;
        }

        // Dựng stack block cùng màu, gắn vào BlocksContainer của prefab (fallback: chính cell).
        private void Fill(TypeColor color, int n)
        {
            Color = color;
            var parent = blocksContainer != null ? blocksContainer : transform;
            for (int j = 0; j < n; j++)
            {
                var b = PoolManager.Instance.GetBlock();
                b.transform.SetParent(parent);
                // Set scale SAU khi SetParent: SetParent(worldPositionStays=true) bù localScale để giữ scale
                // world, nên phải gán đè thì scale của grid mới ăn. Cell không scale → local = world.
                b.transform.localScale = _blockScale;
                b.transform.position = transform.position + Vector3.up * _stackSpacing * j; // stack theo Y
                b.transform.rotation = transform.rotation;
                b.Init(this, j, Color);
                _blocks.Add(b);
            }
        }

        private void ReleaseBlocks()
        {
            foreach (var b in _blocks) if (b != null) b.Despawn();
            _blocks.Clear();
        }

        /// <summary>Trả cell (và toàn bộ block trong nó) về pool — thay cho Destroy.</summary>
        public void Despawn()
        {
            ReleaseBlocks();
            if (_pool != null) _pool.Release(this);
            else Destroy(gameObject);
        }

        /// <summary>Đặt chỗ 1 đạn đang bay tới cell này.</summary>
        public void ReserveHit() => _pendingHits++;

        /// <summary>Đạn tới nơi: trừ 1 pending + phá 1 block.</summary>
        public void ApplyHit()
        {
            if (_pendingHits > 0) _pendingHits--;
            HitOnce();
        }

        private void HitOnce()
        {
            if (_blocks.Count == 0) return;

            int last = _blocks.Count - 1;
            var b = _blocks[last];
            _blocks.RemoveAt(last);
            if (b != null) b.Despawn(); // trả block về pool

            if (_blocks.Count > 0) return;
            if (_manager != null) _manager.OnCellCleared(this);
        }

        public void MoveTo(Vector3 target, float duration)
        {
            if (_moveRoutine != null) StopCoroutine(_moveRoutine);
            if (!gameObject.activeInHierarchy || duration <= 0f)
            {
                transform.position = target;
                PendingEntry = false; // tới nơi ngay → cho ngắm
                return;
            }
            PendingEntry = true; // bắt đầu trượt → tạm khoá ngắm tới khi tới nơi
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
            PendingEntry = false; // đã trượt xong về đúng ô → giờ mới cho gun ngắm
        }
    }
}
