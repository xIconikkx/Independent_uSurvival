// used to destroy structures on death
using UnityEngine;
using Mirror;

public class OnDeathDestroy : NetworkBehaviour
{
	[Server]
	public void OnDeath()
	{
		Destroy(gameObject);
	}
}
