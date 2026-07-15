using UnityEngine;

public class BlockOutline : MonoBehaviour
{
	public MeshRenderer CurMeshRenderer;

	//private BlockOutlineState CurBlockOutlineState;

	//private BlockOutlineState FallbackBlockOutlineState;

	private float ElapsedTime;

	private MaterialPropertyBlock OutlineMPB;

	private static readonly Color SuperHolePreviewOutlineColor;

	private static readonly float SuperHolePreviewOutlineWidth;

	private static readonly Color SuperHoleActiveOutlineColor;

	private static readonly float SuperHoleActiveOutlineMaxWidth;

	private static readonly float SuperHoleActiveOutlineSpeed;

	internal void SetUp(int blockType)
	{
	}

	internal void UpdateBlockOutline()
	{
	}

	//internal void SetOutlineState(BlockOutlineState newState)
	//{
	//}

	internal void ClearOutlineState()
	{
	}

	internal void ResetOutline()
	{
	}

	private void ApplyOutline(Color color, float width)
	{
	}
}
