// based on Unity's FirstPersonController & ThirdPersonController scripts
using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using StandardAssets.Characters.Physics;
using Mirror;

// MoveState as byte for minimal bandwidth (otherwise it's int by default)
// note: distinction between WALKING and RUNNING in case we need to know the
//       difference somewhere (e.g. for endurance recovery)
// note: AIRBORNE means jumping || falling. no need to have two states for that.
public enum MoveState : byte {IDLE, WALKING, RUNNING, CROUCHING, CRAWLING, AIRBORNE, CLIMBING, SWIMMING, DEAD}

[RequireComponent(typeof(OpenCharacterController))]
[RequireComponent(typeof(AudioSource))]
public class PlayerMovement : NetworkBehaviour
{
    // components to be assigned in inspector
    [Header("Components")]
    public Animator animator;
    public Health health;
    public OpenCharacterController controller;
    public AudioSource feetAudio;
    public Combat combat;
    public PlayerLook look;
    public Endurance endurance;
    // the collider for the character controller. NOT the hips collider. this
    // one is NOT affected by animations and generally a better choice for state
    // machine logic.
    public CapsuleCollider controllerCollider;
#pragma warning disable CS0109 // member does not hide accessible member
    new Camera camera;
#pragma warning restore CS0109 // member does not hide accessible member

    [Header("State")]
    public MoveState state = MoveState.IDLE;
    MoveState lastState = MoveState.IDLE;
    [HideInInspector] public Vector3 moveDir;

    // it's useful to have both strafe movement (WASD) and rotations (QE)
    // => like in WoW, it more fun to play this way.
    [Header("Rotation")]
    public float rotationSpeed = 150;

    [Header("Walking")]
    public float walkSpeed = 5;
    public float walkAcceleration = 15; // set to maxint for instant speed
    public float walkDeceleration = 20; // feels best if higher than acceleration

    [Header("Running")]
    public float runSpeed = 8;
    [Range(0f, 1f)] public float runStepLength = 0.7f;
    public float runStepInterval = 3;
    public float runCycleLegOffset = 0.2f; //specific to the character in sample assets, will need to be modified to work with others
    public KeyCode runKey = KeyCode.LeftShift;
    float stepCycle;
    float nextStep;

    [Header("Crouching")]
    public float crouchSpeed = 1.5f;
    public float crouchAcceleration = 5; // set to maxint for instant speed
    public float crouchDeceleration = 10; // feels best if higher than acceleration
    public KeyCode crouchKey = KeyCode.C;
    bool crouchKeyPressed;

    [Header("Crawling")]
    public float crawlSpeed = 1;
    public float crawlAcceleration = 5; // set to maxint for instant speed
    public float crawlDeceleration = 10; // feels best if higher than acceleration
    public KeyCode crawlKey = KeyCode.V;
    bool crawlKeyPressed;

    [Header("Swimming")]
    public float swimSpeed = 4;
    public float swimAcceleration = 15; // set to maxint for instant speed
    public float swimDeceleration = 20; // feels best if higher than acceleration
    public float swimSurfaceOffset = 0.25f;
    Collider waterCollider;
    bool inWater => waterCollider != null; // standing in water / touching it?
    bool underWater; // deep enough in water so we need to swim?
    [Range(0, 1)] public float underwaterThreshold = 0.9f; // percent of body that need to be underwater to start swimming
    public LayerMask canStandInWaterCheckLayers = Physics.DefaultRaycastLayers; // set this to everything except water layer

    [Header("Jumping")]
    public float jumpSpeed = 7;
    [HideInInspector] public float jumpLeg;
    bool jumpKeyPressed;

    [Header("Falling")]
    public float airborneAcceleration = 15; // set to maxint for instant speed
    public float airborneDeceleration = 20; // feels best if higher than acceleration
    public float fallMinimumMagnitude = 6; // walking down steps shouldn't count as falling and play no falling sound.
    public float fallDamageMinimumMagnitude = 13;
    public float fallDamageMultiplier = 2;
    [HideInInspector] public Vector3 lastFall;
    bool sprintingBeforeAirborne; // don't allow sprint key to accelerate while jumping. decision has to be made before that.

    [Header("Climbing")]
    public float climbSpeed = 3;
    Collider ladderCollider;

    [Header("Physics")]
    [Tooltip("Apply a small default downward force while grounded in order to stick on the ground and on rounded surfaces. Otherwise walking on rounded surfaces would be detected as falls, preventing the player from jumping.")]
    public float gravityMultiplier = 2;

    [Header("Synchronization (Best not to modify)")]
    [Tooltip("Buffer at least that many moves before applying them in FixedUpdate. A bigger min buffer offers more lag tolerance with the cost of additional latency.")]
    public int minMoveBuffer = 2;
    [Tooltip("Combine two moves as one after having more than that many pending moves in order to avoid ever growing queues.")]
    public int combineMovesAfter = 5;
    [Tooltip("Buffer at most that many moves before force reseting the client. There is no point in buffering hundreds of moves and having it always lag 3 seconds behind.")]
    public int maxMoveBuffer = 10; // 20ms fixedDelta * 10 = 200 ms. no need to buffer more than that.
    [Tooltip("Rubberband movement: player can move freely as long as the server position matches. If server and client get off further than rubberDistance then a force reset happens.")]
    public float rubberDistance = 1;
    byte route = 0; // used to stamp moves sent to server, see FixedUpdate. byte is enough. it will overflow and that's fine!
    int combinedMoves = 0; // debug information only
    int rubbered = 0; // debug information only

    // we need to remember the last accelerated xz speed without gravity etc.
    // (using moveDir.xz.magnitude doesn't work well with mounted movement)
    float horizontalSpeed;

    // helper property to check grounded with some tolerance. technically we
    // aren't grounded when walking down steps, but this way we factor in a
    // minimum fall magnitude. useful for more tolerant jumping etc.
    // (= while grounded or while velocity not smaller than min fall yet)
    public bool isGroundedWithinTolerance =>
        controller.isGrounded || controller.velocity.y > -fallMinimumMagnitude;

    [Header("Sounds")]
    public AudioClip[] footstepSounds;    // an array of footstep sounds that will be randomly selected from.
    public AudioClip jumpSound;           // the sound played when character leaves the ground.
    public AudioClip landSound;           // the sound played when character touches back on ground.

    [Header("Debug")]
    [Tooltip("Debug GUI visibility curve. X axis = distance, Y axis = alpha. Nothing will be displayed if Y = 0.")]
    public AnimationCurve debugVisibilityCurve = new AnimationCurve(new Keyframe(0, 0.3f), new Keyframe(15, 0.3f), new Keyframe(20, 0f));


    // we can't point to controller.velocity because that might not be reliable
    // over the network if we apply multiple moves at once. instead save the
    // last valid move's velocity here.
    public Vector3 velocity { get; private set; }

    void Awake()
    {
        camera = Camera.main;
    }

    void OnValidate()
    {
        minMoveBuffer = Mathf.Clamp(minMoveBuffer, 1, maxMoveBuffer); // need at least 1 to move
        combineMovesAfter = Mathf.Clamp(combineMovesAfter, minMoveBuffer + 1, maxMoveBuffer);
        maxMoveBuffer = Mathf.Clamp(maxMoveBuffer, combineMovesAfter + 1, 50); // 20ms*50=1s at max should be enough
    }

    // input directions ////////////////////////////////////////////////////////
    Vector2 GetInputDirection()
    {
        // get input direction while alive and while not typing in chat
        // (otherwise 0 so we keep falling even if we die while jumping etc.)
        float horizontal = 0;
        float vertical = 0;
        if (!UIUtils.AnyInputActive())
        {
            horizontal = Input.GetAxis("Horizontal");
            vertical = Input.GetAxis("Vertical");
        }
        return new Vector2(horizontal, vertical).normalized;
    }

    Vector3 GetDesiredDirection(Vector2 inputDir)
    {
        // always move along the camera forward as it is the direction that is being aimed at
        return transform.forward * inputDir.y + transform.right * inputDir.x;
    }

    Vector3 GetDesiredDirectionOnGround(Vector3 desiredDir)
    {
        // get a normal for the surface that is being touched to move along it
        if (Physics.SphereCast(transform.position, controller.radius, Vector3.down, out RaycastHit hitInfo,
                               controller.height/2f, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            return Vector3.ProjectOnPlane(desiredDir, hitInfo.normal).normalized;
        }
        return desiredDir;
    }

    // scale charactercontroller collider to pose, otherwise we can fire above a
    // crawling player and still hit him.
    void AdjustControllerCollider()
    {
        // ratio depends on state
        float ratio = 1;
        if (state == MoveState.CROUCHING)
            ratio = 0.5f;
        else if (state == MoveState.CRAWLING || state == MoveState.SWIMMING || state == MoveState.DEAD)
            ratio = 0.25f;

        controller.SetHeight(controller.defaultHeight * ratio, true, true, false);
    }

    // movement state machine //////////////////////////////////////////////////
    bool EventDied()
    {
        return health.current == 0;
    }

    bool EventJumpRequested()
    {
        // only while grounded, so jump key while jumping doesn't start a new
        // jump immediately after landing
        // => and not while sliding, otherwise we could climb slides by jumping
        // => grounded check uses min fall tolerance so we can actually still
        //    jump when walking down steps.
        return isGroundedWithinTolerance &&
               !controller.isSlidingDownSlope &&
               jumpKeyPressed;
    }

    bool EventCrouchToggle()
    {
        return crouchKeyPressed;
    }

    bool EventCrawlToggle()
    {
        return crawlKeyPressed;
    }

    bool EventFalling()
    {
        // use minimum fall magnitude so walking down steps isn't detected as
        // falling! otherwise walking down steps would show the fall animation
        // and play the landing sound.
        return !isGroundedWithinTolerance;
    }

    bool EventLanded()
    {
        return controller.isGrounded;
    }

    bool EventUnderWater()
    {
        // we can't really make it player position dependent, because he might
        // swim to the surface at which point it might be detected as standing
        // in water but not being under water, etc.
        if (inWater) // in water and valid water collider?
        {
            // raycasting from water to the bottom at the position of the player
            // seems like a very precise solution
            Vector3 origin = new Vector3(transform.position.x,
                                         waterCollider.bounds.max.y,
                                         transform.position.z);
            float distance = controllerCollider.height * underwaterThreshold;
            Debug.DrawLine(origin, origin + Vector3.down * distance, Color.cyan);

            // we are underwater if the raycast doesn't hit anything
            return !Utils.RaycastWithout(origin, Vector3.down, out RaycastHit hit, distance, gameObject, canStandInWaterCheckLayers);
        }
        return false;
    }

    bool EventLadderEnter()
    {
        return ladderCollider != null;
    }

    bool EventLadderExit()
    {
        // OnTriggerExit isn't good enough to detect ladder exits because we
        // shouldn't exit as soon as our head sticks out of the ladder collider.
        // only if we fully left it. so check this manually here:
        return ladderCollider != null &&
               !ladderCollider.bounds.Intersects(controllerCollider.bounds);
    }

    // helper function to apply gravity based on previous Y direction
    float ApplyGravity(float moveDirY)
    {
        // apply full gravity while falling
        if (!controller.isGrounded)
            // gravity needs to be * Time.fixedDeltaTime even though we multiply
            // the final controller.Move * Time.fixedDeltaTime too, because the
            // unit is 9.81m/s²
            return moveDirY + Physics.gravity.y * gravityMultiplier * Time.fixedDeltaTime;
        // if grounded then apply no force. the new OpenCharacterController
        // doesn't need a ground stick force. it would only make the character
        // slide on all uneven surfaces.
        return 0;
    }

    // helper function to get move or walk speed depending on key press & endurance
    float GetWalkOrRunSpeed()
    {
        bool runRequested = !UIUtils.AnyInputActive() && Input.GetKey(runKey);
        return runRequested && endurance.current > 0 ? runSpeed : walkSpeed;
    }

    void ApplyFallDamage()
    {
        // measure only the Y direction. we don't want to take fall damage
        // if we jump forward into a wall because xz is high.
        float fallMagnitude = Mathf.Abs(lastFall.y);
        if(fallMagnitude >= fallDamageMinimumMagnitude)
        {
            int damage = Mathf.RoundToInt(fallMagnitude * fallDamageMultiplier);
            health.current -= damage;
            combat.RpcOnReceivedDamage(damage, transform.position, -lastFall);
        }
    }

    // acceleration can be different when accelerating/decelerating
    float AccelerateSpeed(Vector2 inputDir, float currentSpeed, float targetSpeed, float acceleration)
    {
        // desired speed is between 'speed' and '0'
        float desiredSpeed = inputDir.magnitude * targetSpeed;

        // accelerate speed
        return Mathf.MoveTowards(currentSpeed, desiredSpeed, acceleration * Time.fixedDeltaTime);
    }

    // rotate with QE keys
    void RotateWithKeys()
    {
        if (!UIUtils.AnyInputActive())
        {
            float horizontal2 = Input.GetAxis("Horizontal2");
            transform.Rotate(Vector3.up * horizontal2 * rotationSpeed * Time.fixedDeltaTime);
        }
    }

    void EnterLadder()
    {
        // make player look directly at ladder forward. but we also initialize
        // freelook manually already to overwrite the initial rotation, so
        // that in the end, the camera keeps looking at the same angle even
        // though we did modify transform.forward.
        // note: even though we set the rotation perfectly here, there's
        //       still one frame where it seems to interpolate between the
        //       new and the old rotation, which causes 1 odd camera frame.
        //       this could be avoided by overwriting transform.forward once
        //       more in LateUpdate.
        if (isLocalPlayer)
        {
            look.InitializeFreeLook();
            transform.forward = ladderCollider.transform.forward;
        }
    }

    MoveState UpdateIDLE(Vector2 inputDir, Vector3 desiredDir)
    {
        // QE key rotation
        RotateWithKeys();

        // decelerate from last move (e.g. from jump)
        // (moveDir.xz can be set to 0 to have an interruption when landing)
        horizontalSpeed = AccelerateSpeed(inputDir, horizontalSpeed, 0, walkDeceleration);
        moveDir.x = desiredDir.x * horizontalSpeed;
        moveDir.y = ApplyGravity(moveDir.y);
        moveDir.z = desiredDir.z * horizontalSpeed;

        if (EventDied())
        {
            // rescale capsule
            controller.SetHeight(controller.defaultHeight * 0.25f, true, true, false);
            return MoveState.DEAD;
        }
        else if (EventFalling())
        {
            sprintingBeforeAirborne = false;
            return MoveState.AIRBORNE;
        }
        else if (EventJumpRequested())
        {
            // start the jump movement into Y dir, go to jumping
            // note: no endurance>0 check because it feels odd if we can't jump
            moveDir.y = jumpSpeed;
            sprintingBeforeAirborne = false;
            PlayJumpSound();
            return MoveState.AIRBORNE;
        }
        else if (EventCrouchToggle())
        {
            // rescale capsule
            controller.SetHeight(controller.defaultHeight * 0.5f, true, true, false);
            return MoveState.CROUCHING;
        }
        else if (EventCrawlToggle())
        {
            // rescale capsule
            controller.SetHeight(controller.defaultHeight * 0.25f, true, true, false);
            return MoveState.CRAWLING;
        }
        else if (EventLadderEnter())
        {
            EnterLadder();
            return MoveState.CLIMBING;
        }
        else if (EventUnderWater())
        {
            // rescale capsule
            controller.SetHeight(controller.defaultHeight * 0.25f, true, true, false);
            return MoveState.SWIMMING;
        }
        else if (inputDir != Vector2.zero)
        {
            return MoveState.WALKING;
        }

        return MoveState.IDLE;
    }

    MoveState UpdateWALKINGandRUNNING(Vector2 inputDir, Vector3 desiredDir)
    {
        // QE key rotation
        RotateWithKeys();

        // walk or run?
        float speed = GetWalkOrRunSpeed();

        // move with acceleration (feels better)
        horizontalSpeed = AccelerateSpeed(inputDir, horizontalSpeed, speed, inputDir != Vector2.zero ? walkAcceleration : walkDeceleration);
        moveDir.x = desiredDir.x * horizontalSpeed;
        moveDir.y = ApplyGravity(moveDir.y);
        moveDir.z = desiredDir.z * horizontalSpeed;

        if (EventDied())
        {
            // rescale capsule
            controller.SetHeight(controller.defaultHeight * 0.25f, true, true, false);
            return MoveState.DEAD;
        }
        else if (EventFalling())
        {
            sprintingBeforeAirborne = speed == runSpeed;
            return MoveState.AIRBORNE;
        }
        else if (EventJumpRequested())
        {
            // start the jump movement into Y dir, go to jumping
            // note: no endurance>0 check because it feels odd if we can't jump
            moveDir.y = jumpSpeed;
            sprintingBeforeAirborne = speed == runSpeed;
            PlayJumpSound();
            return MoveState.AIRBORNE;
        }
        else if (EventCrouchToggle())
        {
            // limit speed to crouch speed so we don't decelerate from run speed
            // to crouch speed (hence crouching too fast for a short time)
            // -> not allowing any speed > crouchspeed also makes speedhack
            //    protection easier later on.
            horizontalSpeed = Mathf.Min(horizontalSpeed, crouchSpeed);
            // rescale capsule
            controller.SetHeight(controller.defaultHeight * 0.5f, true, true, false);
            return MoveState.CROUCHING;
        }
        else if (EventCrawlToggle())
        {
            // limit speed to crawl speed so we don't decelerate from run speed
            // to crawl speed (hence crawling too fast for a short time)
            // -> not allowing any speed > crawlspeed also makes speedhack
            //    protection easier later on.
            horizontalSpeed = Mathf.Min(horizontalSpeed, crawlSpeed);
            // rescale capsule
            controller.SetHeight(controller.defaultHeight * 0.25f, true, true, false);
            return MoveState.CRAWLING;
        }
        else if (EventLadderEnter())
        {
            EnterLadder();
            return MoveState.CLIMBING;
        }
        else if (EventUnderWater())
        {
            // rescale capsule
            controller.SetHeight(controller.defaultHeight * 0.25f, true, true, false);
            return MoveState.SWIMMING;
        }
        // go to idle after fully decelerating (y doesn't matter)
        else if (moveDir.x == 0 && moveDir.z == 0)
        {
            return MoveState.IDLE;
        }

        ProgressStepCycle(inputDir, speed);
        return speed == walkSpeed ? MoveState.WALKING : MoveState.RUNNING;
    }

    MoveState UpdateCROUCHING(Vector2 inputDir, Vector3 desiredDir)
    {
        // QE key rotation
        RotateWithKeys();

        // move with acceleration (feels better)
        horizontalSpeed = AccelerateSpeed(inputDir, horizontalSpeed, crouchSpeed, inputDir != Vector2.zero ? crouchAcceleration : crouchDeceleration);
        moveDir.x = desiredDir.x * horizontalSpeed;
        moveDir.y = ApplyGravity(moveDir.y);
        moveDir.z = desiredDir.z * horizontalSpeed;

        if (EventDied())
        {
            // rescale capsule
            controller.SetHeight(controller.defaultHeight * 0.25f, true, true, false);
            return MoveState.DEAD;
        }
        else if (EventFalling())
        {
            // rescale capsule if possible
            if (controller.CanSetHeight(controller.defaultHeight * 1f, true))
            {
                controller.SetHeight(controller.defaultHeight * 1f, true, true, false);
                sprintingBeforeAirborne = false;
                return MoveState.AIRBORNE;
            }
        }
        else if (EventJumpRequested())
        {
            // stop crouching when pressing jump key. this feels better than
            // jumping from the crouching state.

            // rescale capsule if possible
            if (controller.CanSetHeight(controller.defaultHeight * 1f, true))
            {
                controller.SetHeight(controller.defaultHeight * 1f, true, true, false);
                return MoveState.IDLE;
            }
        }
        else if (EventCrouchToggle())
        {
            // rescale capsule if possible
            if (controller.CanSetHeight(controller.defaultHeight * 1f, true))
            {
                controller.SetHeight(controller.defaultHeight * 1f, true, true, false);
                return MoveState.IDLE;
            }
        }
        else if (EventCrawlToggle())
        {
            // limit speed to crawl speed so we don't decelerate from run speed
            // to crawl speed (hence crawling too fast for a short time)
            // -> not allowing any speed > crawlspeed also makes speedhack
            //    protection easier later on.
            horizontalSpeed = Mathf.Min(horizontalSpeed, crawlSpeed);
            // rescale capsule
            controller.SetHeight(controller.defaultHeight * 0.25f, true, true, false);
            return MoveState.CRAWLING;
        }
        else if (EventLadderEnter())
        {
            // rescale capsule if possible
            if (controller.CanSetHeight(controller.defaultHeight * 1f, true))
            {
                controller.SetHeight(controller.defaultHeight * 1f, true, true, false);
                EnterLadder();
                return MoveState.CLIMBING;
            }
        }
        else if (EventUnderWater())
        {
            // rescale capsule
            controller.SetHeight(controller.defaultHeight * 0.25f, true, true, false);
            return MoveState.SWIMMING;
        }

        ProgressStepCycle(inputDir, crouchSpeed);
        return MoveState.CROUCHING;
    }

    MoveState UpdateCRAWLING(Vector2 inputDir, Vector3 desiredDir)
    {
        // QE key rotation
        RotateWithKeys();

        // move with acceleration (feels better)
        horizontalSpeed = AccelerateSpeed(inputDir, horizontalSpeed, crawlSpeed, inputDir != Vector2.zero ? crawlAcceleration : crawlDeceleration);
        moveDir.x = desiredDir.x * horizontalSpeed;
        moveDir.y = ApplyGravity(moveDir.y);
        moveDir.z = desiredDir.z * horizontalSpeed;

        if (EventDied())
        {
            // rescale capsule
            controller.SetHeight(controller.defaultHeight * 0.25f, true, true, false);
            return MoveState.DEAD;
        }
        else if (EventFalling())
        {
            // rescale capsule if possible
            if (controller.CanSetHeight(controller.defaultHeight * 1f, true))
            {
                controller.SetHeight(controller.defaultHeight * 1f, true, true, false);
                sprintingBeforeAirborne = false;
                return MoveState.AIRBORNE;
            }
        }
        else if (EventJumpRequested())
        {
            // stop crawling when pressing jump key. this feels better than
            // jumping from the crawling state.

            // rescale capsule if possible
            if (controller.CanSetHeight(controller.defaultHeight * 1f, true))
            {
                controller.SetHeight(controller.defaultHeight * 1f, true, true, false);
                return MoveState.IDLE;
            }
        }
        else if (EventCrouchToggle())
        {
            // rescale capsule if possible
            if (controller.CanSetHeight(controller.defaultHeight * 0.5f, true))
            {
                // limit speed to crouch speed so we don't decelerate from run speed
                // to crouch speed (hence crouching too fast for a short time)
                // -> not allowing any speed > crouchspeed also makes speedhack
                //    protection easier later on.
                horizontalSpeed = Mathf.Min(horizontalSpeed, crouchSpeed);
                // rescale capsule
                controller.SetHeight(controller.defaultHeight * 0.5f, true, true, false);
                return MoveState.CROUCHING;
            }
        }
        else if (EventCrawlToggle())
        {
            // rescale capsule if possible
            if (controller.CanSetHeight(controller.defaultHeight * 1f, true))
            {
                controller.SetHeight(controller.defaultHeight * 1f, true, true, false);
                return MoveState.IDLE;
            }
        }
        else if (EventLadderEnter())
        {
            // rescale capsule if possible
            if (controller.CanSetHeight(controller.defaultHeight * 1f, true))
            {
                controller.SetHeight(controller.defaultHeight * 1f, true, true, false);
                EnterLadder();
                return MoveState.CLIMBING;
            }
        }
        else if (EventUnderWater())
        {
            // rescale capsule
            controller.SetHeight(controller.defaultHeight * 0.25f, true, true, false);
            return MoveState.SWIMMING;
        }

        ProgressStepCycle(inputDir, crawlSpeed);
        return MoveState.CRAWLING;
    }

    MoveState UpdateAIRBORNE(Vector2 inputDir, Vector3 desiredDir)
    {
        // QE key rotation
        RotateWithKeys();

        // max speed depends on what we did before jumping/falling
        float speed = sprintingBeforeAirborne ? runSpeed : walkSpeed;

        // move with acceleration (feels better)
        horizontalSpeed = AccelerateSpeed(inputDir, horizontalSpeed, speed, inputDir != Vector2.zero ? airborneAcceleration : airborneDeceleration);
        moveDir.x = desiredDir.x * horizontalSpeed;
        moveDir.y = ApplyGravity(moveDir.y);
        moveDir.z = desiredDir.z * horizontalSpeed;

        if (EventDied())
        {
            // rescale capsule
            controller.SetHeight(controller.defaultHeight * 0.25f, true, true, false);
            return MoveState.DEAD;
        }
        else if (EventLanded())
        {
            PlayLandingSound();
            return MoveState.IDLE;
        }
        else if (EventLadderEnter())
        {
            EnterLadder();
            return MoveState.CLIMBING;
        }
        else if (EventUnderWater())
        {
            // rescale capsule
            controller.SetHeight(controller.defaultHeight * 0.25f, true, true, false);
            return MoveState.SWIMMING;
        }

        return MoveState.AIRBORNE;
    }

    MoveState UpdateCLIMBING(Vector2 inputDir, Vector3 desiredDir)
    {
        if (EventDied())
        {
            // player rotation was adjusted to ladder rotation before.
            // let's reset it, but also keep look forward
            transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
            ladderCollider = null;
            // rescale capsule
            controller.SetHeight(controller.defaultHeight * 0.25f, true, true, false);
            return MoveState.DEAD;
        }
        // finished climbing?
        else if (EventLadderExit())
        {
            // player rotation was adjusted to ladder rotation before.
            // let's reset it, but also keep look forward
            transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
            ladderCollider = null;
            return MoveState.IDLE;
        }

        // interpret forward/backward movement as upward/downward
        // note: NO ACCELERATION, otherwise we would climb really fast when
        //       sprinting towards a ladder. and the actual climb feels way too
        //       unresponsive when accelerating.
        moveDir.x = inputDir.x * climbSpeed;
        moveDir.y = inputDir.y * climbSpeed;
        moveDir.z = 0;

        // make the direction relative to ladder rotation. so when pressing right
        // we always climb to the right of the ladder, no matter how it's rotated
        moveDir = ladderCollider.transform.rotation * moveDir;
        Debug.DrawLine(transform.position, transform.position + moveDir, Color.yellow, 0.1f, false);

        return MoveState.CLIMBING;
    }

    MoveState UpdateSWIMMING(Vector2 inputDir, Vector3 desiredDir)
    {
        if (EventDied())
        {
            // rescale capsule
            controller.SetHeight(controller.defaultHeight * 0.25f, true, true, false);
            return MoveState.DEAD;
        }
        // ladder under / above water?
        else if (EventLadderEnter())
        {
            // rescale capsule if possible
            if (controller.CanSetHeight(controller.defaultHeight * 1f, true))
            {
                controller.SetHeight(controller.defaultHeight * 1f, true, true, false);
                EnterLadder();
                return MoveState.CLIMBING;
            }
        }
        // not under water anymore?
        else if (!EventUnderWater())
        {
            // rescale capsule if possible
            if (controller.CanSetHeight(controller.defaultHeight * 1f, true))
            {
                controller.SetHeight(controller.defaultHeight * 1f, true, true, false);
                return MoveState.IDLE;
            }
        }

        // QE key rotation
        RotateWithKeys();

        // move with acceleration (feels better)
        horizontalSpeed = AccelerateSpeed(inputDir, horizontalSpeed, swimSpeed, inputDir != Vector2.zero ? swimAcceleration : swimDeceleration);
        moveDir.x = desiredDir.x * horizontalSpeed;
        moveDir.z = desiredDir.z * horizontalSpeed;

        // gravitate toward surface
        if (waterCollider != null)
        {
            float surface = waterCollider.bounds.max.y;
            float surfaceDirection = surface - controller.bounds.min.y - swimSurfaceOffset;
            moveDir.y = surfaceDirection * swimSpeed;
        }
        else moveDir.y = 0;

        return MoveState.SWIMMING;
    }

    MoveState UpdateDEAD(Vector2 inputDir, Vector3 desiredDir)
    {
        // keep falling while dead: if we get shot while falling, we shouldn't
        // stop in mid air
        moveDir.x = 0;
        moveDir.y = ApplyGravity(moveDir.y);
        moveDir.z = 0;

        // not dead anymore?
        if (health.current > 0)
        {
            // rescale capsule in any case. we don't check CanSetHeight so that
            // no other entities can block a respawn. SetHeight will depenetrate
            // either way.
            controller.SetHeight(controller.defaultHeight * 1f, true, true, false);
            return MoveState.IDLE;
        }
        return MoveState.DEAD;
    }

    // use Update to check Input
    void Update()
    {
        // not while typing in UI etc.
        if (!UIUtils.AnyInputActive())
        {
            if (!jumpKeyPressed) jumpKeyPressed = Input.GetButtonDown("Jump");
            if (!crawlKeyPressed) crawlKeyPressed = Input.GetKeyDown(crawlKey);
            if (!crouchKeyPressed) crouchKeyPressed = Input.GetKeyDown(crouchKey);
        }
    }

    // send each FixedUpdate move to the server to apply as well
    struct Move
    {
        public byte route;
        public MoveState state;
        public Vector3 position; // position is 100% accurate. moveDir alone would get out of sync after a while.
        public byte yRotation; // 1 byte for 0..255 is enough precision
        public Move(byte route, MoveState state, Vector3 position, byte yRotation)
        {
            this.route = route;
            this.state = state;
            this.position = position;
            this.yRotation = yRotation;
        }
    }
    Queue<Move> pendingMoves = new Queue<Move>();
    [Command]
    void CmdFixedMove(Move move)
    {
        // check route to make sure that the move received was sent AFTER the
        // last force reset. we don't want to apply moves that were in transit
        // during the last force reset (we don't want to apply old moves).
        if (move.route != route)
            return;

        // add to pending moves until FixedUpdate happens
        // (not for localplayer, it always moves itself)
        // (not if queue is full. it will be reset in next move anyway, and
        //  there is no need to waste memory with ever growing lists in case
        //  another client or an attacker spams way too frequently)
        if (!isLocalPlayer && pendingMoves.Count < maxMoveBuffer)
            pendingMoves.Enqueue(move);

        // broadcast to all players in proximity
        // (do this for localplayer too. others need to know our moves)
        RpcFixedMove(move);
    }

    [ClientRpc]
    void RpcFixedMove(Move move)
    {
        // note: no need to ignore old routes here because TCP is ordered.
        // all RpcFixedMoves with old routes will arrive BEFORE RpcForceReset.

        // do nothing if host. CmdFixedMove already added to queue.
        if (isServer) return;

        // do nothing if localplayer. it moves itself.
        if (isLocalPlayer) return;

        // add to pending moves until FixedUpdate happens
        // (not if queue is full. it will be reset in next move anyway, and
        //  there is no need to waste memory with ever growing lists in case
        //  another client or an attacker spams way too frequently)
        if (pendingMoves.Count < maxMoveBuffer)
            pendingMoves.Enqueue(move);
    }

    // force reset needs to happen on all clients. they all need to know the
    // player's new route number to disregard old values etc.
    [ClientRpc]
    void RpcForceReset(Vector3 position, byte newRoute)
    {
        // force reset position
        transform.position = position;

        // set new route
        route = newRoute;

        // clear all moves with the old route. only apply the new ones now.
        // (we do this for other players, local player has no queue)
        pendingMoves.Clear();
    }

    [Server]
    public void ForcePosition(Vector3 position)
    {
        // set new position
        transform.position = position;

        // ignore all incoming movements with old route
        ++route;

        // clear buffer so we don't apply old moves
        pendingMoves.Clear();

        // let the clients know
        RpcForceReset(position, route);
        //Debug.LogWarning("Force reset to route=" + route);
    }

    // rubberbanding if position got too far off from expected
    // position. e.g. if we ran into a wall even though the
    // client walked around it.
    // -> can be TESTED easily by adding Vector3.forward to
    //    each CmdFixedMove call! it will be reset when missing
    //    walls.
    [Server]
    void RubberbandCheck(Vector3 expectedPosition)
    {
        if (Vector3.Distance(transform.position, expectedPosition) >= rubberDistance)
        {
            ForcePosition(transform.position);
            ++rubbered; // debug information only
        }
    }

    // CharacterController movement is physics based and requires FixedUpdate.
    // (using Update causes strange movement speeds in builds otherwise)
    void FixedUpdate()
    {
        // only control movement for local player
        if (isLocalPlayer)
        {
            // get input and desired direction based on camera and ground
            Vector2 inputDir = GetInputDirection();
            Vector3 desiredDir = GetDesiredDirection(inputDir);
            Vector3 desiredGroundDir = GetDesiredDirectionOnGround(desiredDir);
            Debug.DrawLine(transform.position, transform.position + new Vector3(inputDir.x, 0, inputDir.y), Color.green);
            Debug.DrawLine(transform.position, transform.position + desiredDir, Color.blue);
            //Debug.DrawLine(transform.position, transform.position + desiredGroundDir, Color.cyan);

            // update state machine
            if (state == MoveState.IDLE)           state = UpdateIDLE(inputDir, desiredGroundDir);
            else if (state == MoveState.WALKING)   state = UpdateWALKINGandRUNNING(inputDir, desiredGroundDir);
            else if (state == MoveState.RUNNING)   state = UpdateWALKINGandRUNNING(inputDir, desiredGroundDir);
            else if (state == MoveState.CROUCHING) state = UpdateCROUCHING(inputDir, desiredGroundDir);
            else if (state == MoveState.CRAWLING)  state = UpdateCRAWLING(inputDir, desiredGroundDir);
            else if (state == MoveState.AIRBORNE)  state = UpdateAIRBORNE(inputDir, desiredGroundDir);
            else if (state == MoveState.CLIMBING)  state = UpdateCLIMBING(inputDir, desiredGroundDir);
            else if (state == MoveState.SWIMMING)  state = UpdateSWIMMING(inputDir, desiredGroundDir);
            else if (state == MoveState.DEAD)      state = UpdateDEAD(inputDir, desiredGroundDir);
            else Debug.LogError("Unhandled Movement State: " + state);

            // cache this move's state to detect landing etc. next time
            if (!controller.isGrounded) lastFall = controller.velocity;

            // move depending on latest moveDir changes
            controller.Move(moveDir * Time.fixedDeltaTime); // note: returns CollisionFlags if needed
            velocity = controller.velocity; // for animations and fall damage

            // broadcast to server
            byte rotationByte = FloatBytePacker.ScaleFloatToByte(transform.rotation.eulerAngles.y, 0, 360, byte.MinValue, byte.MaxValue); // compress rotation
            CmdFixedMove(new Move(route, state, transform.position, rotationByte));

            // calculate which leg is behind, so as to leave that leg trailing in the jump animation
            // (This code is reliant on the specific run cycle offset in our animations,
            // and assumes one leg passes the other at the normalized clip times of 0.0 and 0.5)
            float runCycle = Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime + runCycleLegOffset, 1);
            jumpLeg = (runCycle < 0.5f ? 1 : -1);// * move.z;

            // reset keys no matter what
            jumpKeyPressed = false;
            crawlKeyPressed = false;
            crouchKeyPressed = false;
        }
        // server/other clients need to do some caching and scaling too
        else
        {
            // scale character collider to pose if not local player.
            // -> correct collider is needed both on server and on clients
            //
            // IMPORTANT: only when switching states. if we do it all the time then
            // a build client's movement speed would be significantly reduced.
            // (and performance would be worse too)
            //
            // scale BEFORE moving. we do the same in the localPlayer state
            // machine above! if we scale after moving then server and client
            // might end up with different results.
            if (lastState != state)
                AdjustControllerCollider();

            // assign lastFall to .velocity (works on server), not controller.velocity
            // BEFORE doing the next move, just like we do for the local player
            if (!controller.isGrounded) lastFall = velocity;

            // apply next pending move if we have at least 'minMoveBuffer'
            // moves pending to make sure that we also still have one to apply
            // in the next FixedUpdate call.
            // => it's better to apply 1,1,1,1 move instead of 2,0,2,1,0 moves.
            if (pendingMoves.Count > 0 && pendingMoves.Count >= minMoveBuffer)
            {
                // too many pending moves?
                // (only the server has authority to reset!)
                if (isServer && pendingMoves.Count >= maxMoveBuffer)
                {
                    // force reset
                    ForcePosition(transform.position);
                }
                // more than combine threshold?
                // (both server and client can combine moves if needed)
                else if (pendingMoves.Count >= 2 && pendingMoves.Count >= combineMovesAfter)
                {
                    // combine the next two moves to minimize overall delay.
                    // => if we are always behind 5 moves then we might as well
                    //    accelerate a bit to always be behind 4, 3, or 2 moves
                    //    to minimize movement delay.
                    //
                    // note: calling controller.Move() multiple times in one
                    //       FixedUpdate doesn't work well and isn't
                    //       recommended. adding the two vectors together works
                    //       better.
                    //
                    // note: we COULD warp to first.position and then move to
                    //       second.position to avoid the double-move which
                    //       always comes with the risk of getting out of sync
                    //       because A,B went behind a wall while A+B went into
                    //       the wall.
                    //
                    //       BUT if we do warp then we would ALWAYS risk wall
                    //       hack cheats since the server would sometimes set
                    //       the position without checking physics via .Move.
                    //
                    //       INSTEAD we risk move(A+B) running into a wall and
                    //       reset if rubberbanding gets too far off. at least
                    //       this method can be made cheat safe.
                    Move first = pendingMoves.Dequeue();
                    Move second = pendingMoves.Dequeue();
                    state = second.state;
                    Vector3 move = second.position - transform.position; // calculate the delta before each move. using .position is 100% accurate and never gets out of sync.
                    velocity = second.position - first.position; // multiple moves. use velocity between the two moves, not between position and second move.
                    float yRotation = FloatBytePacker.ScaleByteToFloat(second.yRotation, byte.MinValue, byte.MaxValue, 0, 360);
                    transform.rotation = Quaternion.Euler(0, yRotation, 0);
                    controller.Move(move);
                    ++combinedMoves; // debug information only

                    // rubberbanding check if has authority
                    if (isServer)
                        RubberbandCheck(second.position);
                }
                // less than combine threshold?
                else
                {
                    // apply one move
                    Move next = pendingMoves.Dequeue();
                    state = next.state;
                    Vector3 move = next.position - transform.position; // calculate the delta before each move. using .position is 100% accurate and never gets out of sync.
                    float yRotation = FloatBytePacker.ScaleByteToFloat(next.yRotation, byte.MinValue, byte.MaxValue, 0, 360);
                    transform.rotation = Quaternion.Euler(0, yRotation, 0);
                    controller.Move(move);
                    velocity = controller.velocity; // only one move, so controller velocity is true.

                    // rubberbanding check if has authority
                    if (isServer)
                        RubberbandCheck(next.position);
                }
            }
            //else Debug.Log("none pending. client should send faster...");
        }

        // some server logic
        if (isServer)
        {
            // apply fall damage only in AIRBORNE state. not when running head
            // forward into a wall with high velocity, etc. we don't ever want
            // to get fall damage while running.
            // -> can't rely on EventLanded here because we don't know if we
            //    receive the client's new state exactly when landed.
            if (lastState == MoveState.AIRBORNE && state != MoveState.AIRBORNE)
            {
                ApplyFallDamage();
            }
        }

        // set last state after everything else is done.
        lastState = state;
    }

    void OnGUI()
    {
        // show data next to player for easier debugging. this is very useful!
        // IMPORTANT: this is basically an ESP hack for shooter games.
        //            DO NOT make this available with a hotkey in release builds
        if (Debug.isDebugBuild &&
            ((NetworkManagerSurvival)NetworkManager.singleton).showDebugGUI)
        {
            // project player position to screen
            Vector3 center = controllerCollider.bounds.center;
            Vector3 point = camera.WorldToScreenPoint(center);

            // sample visibility curve based on distance. avoid GUI calls if
            // alpha = 0 at this distance.
            float distance = Vector3.Distance(camera.transform.position, transform.position);
            float alpha = debugVisibilityCurve.Evaluate(distance);

            // enough alpha, in front of camera and in screen?
            if (alpha > 0 && point.z >= 0 && Utils.IsPointInScreen(point))
            {
                GUI.color = new Color(0, 0, 0, alpha);
                GUILayout.BeginArea(new Rect(point.x, Screen.height - point.y, 150, 100));
                GUILayout.Label("grounded=" + controller.isGrounded);
                GUILayout.Label("groundedT=" + isGroundedWithinTolerance);
                GUILayout.Label("lastFall=" + lastFall.y);
                if (!isLocalPlayer)
                {
                    GUILayout.Label("health=" + health.current + "/" + health.max);
                    GUILayout.Label("pending=" + pendingMoves.Count);
                    GUILayout.Label("route=" + route);
                    GUILayout.Label("combined=" + combinedMoves);
                }
                if (isServer) GUILayout.Label("rubbered=" + rubbered);
                GUILayout.EndArea();
                GUI.color = Color.white;
            }
        }
    }

    void PlayLandingSound()
    {
        feetAudio.clip = landSound;
        feetAudio.Play();
        nextStep = stepCycle + .5f;
    }

    void PlayJumpSound()
    {
        feetAudio.clip = jumpSound;
        feetAudio.Play();
    }

    void ProgressStepCycle(Vector3 inputDir, float speed)
    {
        if (controller.velocity.sqrMagnitude > 0 && (inputDir.x != 0 || inputDir.y != 0))
        {
            stepCycle += (controller.velocity.magnitude + (speed*(state == MoveState.WALKING ? 1 : runStepLength)))*
                         Time.fixedDeltaTime;
        }

        if (stepCycle > nextStep)
        {
            nextStep = stepCycle + runStepInterval;
            PlayFootStepAudio();
        }
    }

    void PlayFootStepAudio()
    {
        if (!controller.isGrounded) return;

        // pick & play a random footstep sound from the array,
        // excluding sound at index 0
        int n = Random.Range(1, footstepSounds.Length);
        feetAudio.clip = footstepSounds[n];
        feetAudio.PlayOneShot(feetAudio.clip);

        // move picked sound to index 0 so it's not picked next time
        footstepSounds[n] = footstepSounds[0];
        footstepSounds[0] = feetAudio.clip;
    }

    [ClientCallback] // client authoritative movement, don't do this on Server
    //[ServerCallback] <- disabled for now, since movement is client authoritative
    void OnTriggerEnter(Collider co)
    {
        // touching ladder? then set ladder collider
        if (co.tag == "Ladder")
            ladderCollider = co;
        // touching water? then set water collider
        else if (co.tag == "Water")
            waterCollider = co;
    }

    [ClientCallback] // client authoritative movement, don't do this on Server
    void OnTriggerExit(Collider co)
    {
        if (co.tag == "Water")
            waterCollider = null;
    }
}
