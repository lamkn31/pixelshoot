using UnityEngine;

namespace Wayfu.Lamkn
{
#region IResetComponent

public interface IResetComponent
{
    void ResetComponent();

    public static void TryResetComponent(Component component)
    {
        if (component == null) return;
        if (component is IResetComponent resetable)
        {
            resetable.ResetComponent();
        }
    }
}

#endregion

#region IItemPool<T>

public interface IItemPool<TItem> where TItem : MonoBehaviour
{
    void OnInitializedInPool(Pooler<TItem> pool);

    public static void TryInitialized(TItem item, Pooler<TItem> pool)
    {
        if (item == null) return;
        if (item is IItemPool<TItem> poolItem)
        {
            poolItem.OnInitializedInPool(pool);
        }
    }
}

#endregion

#region ISetDatable

public interface ISetDatable
{
    void SetData(object data);

    public static void TrySetData(Component component, object data)
    {
        if (component == null) return;
        if (component is ISetDatable setable)
        {
            setable.SetData(data);
        }
    }
}

#endregion
}
