using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;

namespace Wayfu.Lamkn
{
[System.Serializable]
public class Pooler<TItem> where TItem : MonoBehaviour
{
    // Collection checks will throw errors if we try to release an item that is already in the pool.
    [SerializeField] private bool collectionChecks = true;
    [SerializeField] private int maxPoolSize = 10;
    [SerializeField] private TItem itemPrefab;
    [SerializeField] private Transform parent;

    public Transform Parent => parent;
    public TItem ItemPrefab => itemPrefab;

    IObjectPool<TItem> m_Pool;
    private List<TItem> _externalPool = new();
    public List<TItem> ExternalPool => _externalPool;

    public void SetPrefab(TItem item)
    {
        itemPrefab = item;
    }



    #region Initation method pool
    private TItem CreatePooledItem()
    {
        var go = MonoBehaviour.Instantiate(itemPrefab, parent);
        go.name = string.Format("Pool_{0}_{1}", itemPrefab.name, UnityEngine.Random.Range(10000,99999));
        return go;
    }

    // Called when an item is returned to the pool using Release
    private void OnReturnedToPool(TItem system)
    {
        if (system == null || system.gameObject == null)
        {
            Debug.LogError("OnReturnedToPool()");
            return;
        }

        system.gameObject.SetActive(false);
        system.transform.SetParent(parent);
        if (_externalPool.Contains(system)) _externalPool.Remove(system);
    }

    // Called when an item is taken from the pool using Get
    private void OnTakeFromPool(TItem system)
    {
        system.gameObject.SetActive(true);
        if (!_externalPool.Contains(system)) _externalPool.Add(system);
    }

    // If the pool capacity is reached then any items returned will be destroyed.
    // We can control what the destroy behavior does, here we destroy the GameObject.
    private void OnDestroyPoolObject(TItem system)
    {
        MonoBehaviour.Destroy(system.gameObject);
    }
    #endregion

    #region Implement method IObjectPool
    private void InitIfNeeded()
    {
        if (m_Pool == null)
        {
            m_Pool = new ObjectPool<TItem>(
                        CreatePooledItem,
                        OnTakeFromPool,
                        OnReturnedToPool,
                        OnDestroyPoolObject,
                        collectionChecks, 10, maxPoolSize);
        }
        if (_externalPool == null) _externalPool = new List<TItem>();

    }
    public void Clear()
    {
        InitIfNeeded();
        m_Pool.Clear();
    }
    public TItem Get()
    {
        InitIfNeeded();
        var system = m_Pool.Get();
        IResetComponent.TryResetComponent(system);
        IItemPool<TItem>.TryInitialized(system, this);
        return system;
    }
    public TItem GetByInstantiate()
    {
        InitIfNeeded();
        var system = UnityEngine.Object.Instantiate(itemPrefab, parent);
        if (!_externalPool.Contains(system)) _externalPool.Add(system);
        IResetComponent.TryResetComponent(system);
        IItemPool<TItem>.TryInitialized(system, this);
        return system;
    }
    public void ReleaseByInstantiate(TItem system)
    {
        if (system == null || system.gameObject == null)
        {
            Debug.LogError("OnReturnedToPool()");
            return;
        }

        if (_externalPool.Contains(system)) _externalPool.Remove(system);
        UnityEngine.Object.Destroy(system.gameObject);
    }
    public void ReturnAllByInstantiate()
    {
        InitIfNeeded();
        var newExternalPool = new List<TItem>();
        newExternalPool.AddRange(_externalPool);
        foreach (var item in newExternalPool)
        {
            ReleaseByInstantiate(item);
        }
    }
    public TItem GetAsLastSibling()
    {
        var item = Get();
        item.transform.SetAsLastSibling();
        return item; 
    }
    public TItem GetAsFirstSibling()
    {
        var item = Get();
        item.transform.SetAsFirstSibling();
        return item; 
    }
    public void LoadDataAsScroller(List<object> data, Action<TItem, object> OnInitItem)
    {
        ReturnAll();
        foreach (var itemData in data)
        {
            var item = GetAsLastSibling();
            OnInitItem?.Invoke(item, itemData);
        }
    }

    public static void OnInitItemBySetData(TItem item, object itemData)
    {
        ISetDatable.TrySetData(item, itemData);
    }
    public void Release(TItem system)
    {
        m_Pool.Release(system);
    }
    public void Releases(params TItem[] systems)
    {
        foreach(var system in systems)
        {
            m_Pool.Release(system);
        }
    }
    public void ReleaseLast()
    {
        if(_externalPool.Count == 0)
        {
            return;
        }
        var system = _externalPool.Last();
        m_Pool.Release(system);
    }
    #endregion

    #region Extension
    public void ReturnAll()
    {
        InitIfNeeded();
        var newExternalPool = new List<TItem>();
        newExternalPool.AddRange(_externalPool);
        foreach (var item in newExternalPool)
        {
            Release(item);
        }
    }
    public void ReturnAllAsStart()
    {
        InitIfNeeded();
        for (int i = _externalPool.Count - 1; i >= 0; i--)
        {
            Release(_externalPool[i]);
        }
    }

    #endregion

    
    /// <summary>
    /// Áp dụng hành động được chỉ định cho tất cả các mục trong _externalPool.
    /// Phương thức này sẽ duyệt qua tất cả các phần tử trong external pool và thực thi hành động 
    /// (được truyền qua đối số action) cho mỗi phần tử.
    /// </summary>
    /// <param name="action">Hành động cần thực thi đối với mỗi mục trong pool.</param>
    public void ApplyExternalPool(Action<TItem> action)
    {
        foreach (var item in _externalPool)
        {
            action?.Invoke(item);
        }
    }

    public void SetParent(Transform parent)
    {
        this.parent = parent;
    }
    public void SetItemPrefab(TItem itemPrefab)
    {
        this.itemPrefab = itemPrefab;
    }

    public List<TItem> GetNewExternalPoolList()
    {
        var list = new List<TItem>();
        list.AddRange(_externalPool);
        return list;
    }
    /// <summary>
    /// RectTransform của object cha.
    /// Được cache lại để tránh gọi GetComponent nhiều lần.
    /// </summary>
    private RectTransform _cachedParentRect;

    /// <summary>
    /// Lấy RectTransform của parent.
    /// Nếu chưa được cache thì sẽ tự động tìm và lưu lại.
    /// </summary>
    public RectTransform CachedParentRect
    {
        get
        {
            if (_cachedParentRect == null)
            {
                _cachedParentRect = parent.GetComponent<RectTransform>();
            }

            return _cachedParentRect;
        }
    }


    /// <summary>
    /// Lấy item theo index trong external pool.
    /// Nếu chưa có đủ, sẽ lấy mới từ pool để đảm bảo đủ số lượng.
    /// </summary>
    public TItem GetNewByIndexOrCreate(int index)
    {
        // Nếu index đã có trong external pool thì trả về item
        if (index < _externalPool.Count)
            return _externalPool[index];

        // Nếu chưa đủ, tạo các item mới cho đến khi đủ
        while (_externalPool.Count <= index)
        {
            var newItem = GetAsLastSibling(); // lấy từ pool
            newItem.transform.SetParent(parent, false); // đặt parent mặc định
        }

        return _externalPool[index];
    }


    /// <summary>
    /// Duyệt tất cả item trong pool và rebuild layout nếu item hỗ trợ.
    /// </summary>
    internal void RebuildAllItemsInPool()
    {
        foreach (var item in ExternalPool)
        {
            RebuildLayoutHelper.TryRebuild(item);
        }
    }


    public TItem GetAsLastSiblingByInstantiate()
    {
        var item = UnityEngine.Object.Instantiate(itemPrefab, parent);
        _externalPool.Add(item);
        item.transform.SetAsLastSibling();
        return item;
    }

    public void ExcuteOnAllExternalPoolItems(Action<TItem> action)
    {
        foreach (var item in _externalPool)
        {
            action?.Invoke(item);
        }
    }
}




/// <summary>
/// Interface cho các component có khả năng tự rebuild layout.
/// </summary>
public interface IRebuildableLayout
{
    /// <summary>
    /// Thực hiện rebuild layout cho component này.
    /// </summary>
    void RebuildLayout();
}

/// <summary>
/// Helper static để gọi rebuild layout an toàn từ bất kỳ Component nào.
/// </summary>
public static class RebuildLayoutHelper
{
    /// <summary>
    /// Thử gọi RebuildLayout nếu component implement IRebuildableLayout.
    /// </summary>
    /// <param name="component">Component cần rebuild layout.</param>
    public static void TryRebuild(Component component)
    {
        if (component is IRebuildableLayout rebuildable)
        {
            rebuildable.RebuildLayout();
        }
    }
}
}
