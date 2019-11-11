using UnityEngine;
using Mirror;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(FiniteStateMachine))]
public class MonsterAnimation : NetworkBehaviour
{
    // Used components. Assign in Inspector. Easier than GetComponent caching.
    public NavMeshAgent agent;
    public FiniteStateMachine fsm;

    [ClientCallback] // no need for animations on the server
    void LateUpdate()
    {
        // pass parameters to animation state machine
        // => passing the states directly is the most reliable way to avoid all
        //    kinds of glitches like movement sliding, attack twitching, etc.
        // => make sure to import all looping animations like idle/run/attack
        //    with 'loop time' enabled, otherwise the client might only play it
        //    once
        // => only play moving animation while the agent is actually moving. the
        //    MOVING state might be delayed to due latency or we might be in
        //    MOVING while a path is still pending, etc.
        // now pass parameters after any possible rebinds
        foreach (Animator anim in GetComponentsInChildren<Animator>())
        {
            anim.SetBool("MOVING", fsm.state == "MOVING" && agent.velocity != Vector3.zero);
            anim.SetFloat("Speed", agent.speed);
            anim.SetBool("ATTACKING", fsm.state == "ATTACKING");
            anim.SetBool("DEAD", fsm.state == "DEAD");
        }
    }
}