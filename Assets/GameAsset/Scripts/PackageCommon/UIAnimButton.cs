using UnityEngine;
using DG.Tweening;
using UnityEngine.EventSystems;

public class UIAnimButton : MonoBehaviour, IPointerUpHandler, IPointerDownHandler
{
    public Vector3 btnScale = new Vector3(0.95f, 0.95f, 0.95f);

    public virtual void OnPointerDown(PointerEventData eventData)
    {
        transform.DOScale(btnScale, 0.15f)
        .SetUpdate(true);
    }

    public virtual void OnPointerUp(PointerEventData eventData)
    {
        transform.DOScale(Vector3.one, 0.15f)
      .SetUpdate(true);
    }
}

