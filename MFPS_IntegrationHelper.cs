using UnityEngine;
using MFPS.InputManager;
using FC_ParkourSystem;
using Photon.Pun;
using System.Collections;

namespace FC_ParkourSystem
{
    public class MFPS_IntegrationHelper : MonoBehaviour, IParkourCharacter
    {
        private ParkourController parkourController;
        private ClimbController climbController;
        private bl_FirstPersonController firstPersonController;
        private bl_PlayerAnimationsBase playerAnimations;
        private bl_PlayerRagdollBase playerRagdoll;
        private Collider playerCollider;
        private bl_PlayerCameraSwitcher cameraSwitcher;
        private bl_PlayerNetwork playerNetwork;
        private bl_PlayerReferences playerReferences;
        private Camera playerCamera;
        private PlayerState lastBodyState;

        public bool UseRootMotion { get; set; } = false;

        public Vector3 MoveDir => new Vector3(bl_GameInput.Horizontal, 0, bl_GameInput.Vertical).normalized;

        public bool IsGrounded => firstPersonController.isGrounded;

        public float Gravity => -9.81f;

        public Animator Animator
        {
            get => playerReferences.PlayerAnimator;
            set => playerReferences.PlayerAnimator = value;
        }

        public bool PreventParkourAction => firstPersonController.State == PlayerState.Crouching || firstPersonController.State == PlayerState.Running;

        private void Awake()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            Debug.Log("Initializing components in MFPS_IntegrationHelper");

            playerReferences = GetComponent<bl_PlayerReferences>();
            if (playerReferences == null)
            {
                Debug.LogError("PlayerReferences is missing.");
                this.enabled = false;
                return;
            }

            parkourController = GetComponent<ParkourController>();
            climbController = GetComponent<ClimbController>();
            firstPersonController = playerReferences.firstPersonController as bl_FirstPersonController;
            playerAnimations = playerReferences.playerAnimations;
            playerRagdoll = playerReferences.playerRagdoll;
            playerCollider = playerReferences.characterController;
            cameraSwitcher = GetComponent<bl_PlayerCameraSwitcher>();
            playerNetwork = playerReferences.playerNetwork;
            playerCamera = playerReferences.playerCamera;

            if (AreRequiredComponentsMissing())
            {
                Debug.LogError("Missing required components for MFPS_IntegrationHelper.");
                this.enabled = false;
                return;
            }

            InitializeAnimatorHelper();
        }

        private bool AreRequiredComponentsMissing()
        {
            return parkourController == null || climbController == null || firstPersonController == null ||
                   playerAnimations == null || playerRagdoll == null || playerCollider == null ||
                   cameraSwitcher == null || playerNetwork == null || playerCamera == null;
        }

        private void InitializeAnimatorHelper()
        {
            // Implementation for initializing animator helper if needed
        }

        private void Update()
        {
            HandleInputs();
            UpdateAnimatorParameters(); // Update the Animator parameters

            if (parkourController != null && !parkourController.ControlledByParkour)
            {
                firstPersonController.OnUpdate();
            }
        }

        private void FixedUpdate()
        {
            if (parkourController != null && parkourController.ControlledByParkour)
            {
                firstPersonController.mouseLook.Update();
                firstPersonController.mouseLook.UpdateLook(firstPersonController.transform, firstPersonController.headRoot);
            }
            else if (firstPersonController != null)
            {
                firstPersonController.FixedUpdate();
            }
            UpdateCamera();
        }

        private void HandleInputs()
        {
            float horizontal = bl_Input.HorizontalAxis;
            float vertical = bl_Input.VerticalAxis;
            bool jump = bl_GameInput.Jump();
            bool crouch = bl_GameInput.Crouch();
            bool drop = bl_GameInput.Interact(); // Assuming Interact is mapped to Drop
            bool sprint = bl_GameInput.Run();

            Vector3 move = new Vector3(horizontal, 0, vertical);
            Animator.SetFloat("Speed", move.magnitude);

            if (move != Vector3.zero)
            {
                MoveCharacter(horizontal, vertical, sprint);
            }

            if (jump)
            {
                HandleJump();
            }

            if (crouch)
            {
                HandleCrouch();
            }

            if (drop)
            {
                HandleDrop();
            }

            // Sliding logic
            if (sprint && crouch && firstPersonController.isGrounded && firstPersonController.Velocity.magnitude > firstPersonController.WalkSpeed)
            {
                HandleSlide();
            }

            // Update the BodyState parameter based on the player's current state
            UpdateBodyState();
        }

        private void MoveCharacter(float horizontal, float vertical, bool sprint)
        {
            if (firstPersonController != null)
            {
                firstPersonController.MovementInput();
                if (sprint)
                {
                    firstPersonController.State = PlayerState.Running;
                }
                else
                {
                    firstPersonController.State = PlayerState.Walking;
                }
            }
        }

        private void HandleJump()
        {
            if (firstPersonController != null && firstPersonController.isGrounded && parkourController != null)
            {
                Animator.SetTrigger("Jump");
                parkourController.VerticalJump();
                firstPersonController.State = PlayerState.Jumping; // Update state to Jumping
            }
        }

        private void HandleCrouch()
        {
            if (firstPersonController != null)
            {
                if (firstPersonController.Crounching)
                {
                    firstPersonController.State = PlayerState.Crouching;
                }
                else
                {
                    firstPersonController.State = PlayerState.Walking;
                }
            }
        }

        private void HandleDrop()
        {
            if (climbController != null && !parkourController.InAction)
            {
                if (bl_Input.isButtonDown("Drop"))
                {
                    if (climbController.envScanner.DropLedgeCheck(transform.forward, out ClimbLedgeData ledgeData))
                    {
                        var currentLedge = ledgeData.ledgeHit.transform;
                        var newPoint = climbController.GetNearestPoint(currentLedge, ledgeData.ledgeHit.point, false, obstacleCheck: false);

                        if (newPoint == null) return;

                        var currentPoint = newPoint;

                        OnStartParkourAction();

                        if (climbController.CheckWall(currentPoint).Value.isWall)
                        {
                            Animator.SetFloat("freeHang", 0);
                            StartCoroutine(MoveToTarget(currentPoint.transform, "DropToHang", 0.50f, 0.90f, rotateToLedge: true, matchStart: AvatarTarget.LeftFoot));
                            firstPersonController.State = PlayerState.Climbing; // Update state to Climbing
                        }
                        else
                        {
                            Animator.SetFloat("freeHang", 1);
                            StartCoroutine(MoveToTarget(currentPoint.transform, "DropToFreeHang", 0.50f, 0.89f, rotateToLedge: true, matchStart: AvatarTarget.LeftFoot));
                            firstPersonController.State = PlayerState.Climbing; // Update state to Climbing
                        }
                        // Set BodyState to ClimbUp when climbing up
                        Animator.SetInteger("BodyState", (int)PlayerState.ClimbUp);
                    }
                }
            }
        }

        private void HandleSlide()
        {
            if (firstPersonController != null && firstPersonController.isGrounded)
            {
                firstPersonController.DoSlide();
                firstPersonController.State = PlayerState.Sliding;
                Debug.Log("Sliding initiated");
            }
        }

        private IEnumerator MoveToTarget(Transform target, string animationState, float matchStartTime, float matchTargetTime, bool rotateToLedge, AvatarTarget matchStart)
        {
            Animator.CrossFade(animationState, 0.1f);
            while (Animator.GetCurrentAnimatorStateInfo(0).IsName(animationState) == false)
            {
                yield return null;
            }
            Animator.MatchTarget(target.position, target.rotation, matchStart, new MatchTargetWeightMask(Vector3.one, 1f), matchStartTime, matchTargetTime);
            while (Animator.IsInTransition(0) || Animator.GetCurrentAnimatorStateInfo(0).normalizedTime < matchTargetTime)
            {
                transform.position = Vector3.Lerp(transform.position, target.position, Time.deltaTime * 5f);
                yield return null;
            }
            OnEndParkourAction();
        }

        private void UpdateCamera()
        {
            // Smooth camera follow
            if (playerCamera != null && firstPersonController != null)
            {
                Vector3 targetPosition = firstPersonController.transform.position + firstPersonController.transform.up * 1.5f - firstPersonController.transform.forward * 1.5f;
                playerCamera.transform.position = Vector3.Lerp(playerCamera.transform.position, targetPosition, Time.deltaTime * 10f); // Increase the lerp factor for smoother transitions
            }
        }

        public void OnEndParkourAction()
        {
            if (firstPersonController != null) firstPersonController.enabled = true;
            if (playerCollider != null) playerCollider.enabled = true;
            if (cameraSwitcher != null) cameraSwitcher.enabled = true;
            if (playerRagdoll != null)
            {
                playerRagdoll.SetActiveRagdollPhysics(false);
            }

            if (playerReferences.gunManager != null)
            {
                playerReferences.gunManager.ReleaseWeapons(false);
            }
        }

        public void OnStartParkourAction()
        {
            if (firstPersonController != null)
            {
                firstPersonController.Velocity = Vector3.zero;
                firstPersonController.enabled = false;
            }
            if (playerCollider != null) playerCollider.enabled = false;
            if (cameraSwitcher != null) cameraSwitcher.enabled = false;
            if (playerRagdoll != null)
            {
                playerRagdoll.SetActiveRagdollPhysics(true);
            }

            if (playerReferences.gunManager != null)
            {
                playerReferences.gunManager.BlockAllWeapons();
            }
        }

        public IEnumerator HandleVerticalJump()
        {
            if (firstPersonController != null && firstPersonController.isGrounded)
            {
                firstPersonController.DoJump();
                firstPersonController.State = PlayerState.Jumping;
            }
            yield break;
        }

        private void UpdateBodyState()
        {
            switch (firstPersonController.State)
            {
                case PlayerState.Idle:
                    Animator.SetInteger("BodyState", 0);
                    break;
                case PlayerState.Walking:
                    Animator.SetInteger("BodyState", 1);
                    break;
                case PlayerState.Running:
                    Animator.SetInteger("BodyState", 2);
                    break;
                case PlayerState.Crouching:
                    Animator.SetInteger("BodyState", 3);
                    break;
                case PlayerState.Jumping:
                    Animator.SetInteger("BodyState", 4);
                    break;
                case PlayerState.Climbing:
                    Animator.SetInteger("BodyState", 5);
                    break;
                case PlayerState.Sliding:
                    Animator.SetInteger("BodyState", 6);
                    break;
                case PlayerState.Dropping:
                    Animator.SetInteger("BodyState", 7);
                    break;
                case PlayerState.Gliding:
                    Animator.SetInteger("BodyState", 8);
                    break;
                case PlayerState.InVehicle:
                    Animator.SetInteger("BodyState", 9);
                    break;
                case PlayerState.Stealth:
                    Animator.SetInteger("BodyState", 10);
                    break;
                case PlayerState.VaultOver:
                    Animator.SetInteger("BodyState", 11);
                    break;
                case PlayerState.VaultOn:
                    Animator.SetInteger("BodyState", 12);
                    break;
                case PlayerState.MediumStepUp:
                    Animator.SetInteger("BodyState", 13);
                    break;
                case PlayerState.ClimbUp:
                    Animator.SetInteger("BodyState", 14);
                    break;
                case PlayerState.StepUp:
                    Animator.SetInteger("BodyState", 15);
                    break;
                case PlayerState.MediumStepUpM:
                    Animator.SetInteger("BodyState", 16);
                    break;
                case PlayerState.LandFromFall:
                    Animator.SetInteger("BodyState", 17);
                    break;
                case PlayerState.LandAndStepForward:
                    Animator.SetInteger("BodyState", 18);
                    break;
                case PlayerState.LandOnSpot:
                    Animator.SetInteger("BodyState", 19);
                    break;
                case PlayerState.FallingToRoll:
                    Animator.SetInteger("BodyState", 20);
                    break;
                case PlayerState.FreeHangClimb:
                    Animator.SetInteger("BodyState", 21);
                    break;
                case PlayerState.BracedHangClimb:
                    Animator.SetInteger("BodyState", 22);
                    break;
                case PlayerState.JumpDown:
                    Animator.SetInteger("BodyState", 23);
                    break;
                case PlayerState.JumpFromHang:
                    Animator.SetInteger("BodyState", 24);
                    break;
                case PlayerState.JumpFromFreeHang:
                    Animator.SetInteger("BodyState", 25);
                    break;
                case PlayerState.BracedHangTryJumpUp:
                    Animator.SetInteger("BodyState", 26);
                    break;
                case PlayerState.WallRun:
                    Animator.SetInteger("BodyState", 27);
                    break;
                default:
                    Animator.SetInteger("BodyState", 0);
                    break;
            }

            Debug.Log($"Updated BodyState to {firstPersonController.State} ({Animator.GetInteger("BodyState")})");
        }

        private void UpdateAnimatorParameters()
        {
            if (Animator != null)
            {
                Animator.SetFloat("Speed", firstPersonController.Velocity.magnitude);
                Animator.SetBool("IsGrounded", firstPersonController.isGrounded);
                Animator.SetBool("IsJumping", firstPersonController.State == PlayerState.Jumping);
                Animator.SetFloat("VerticalSpeed", firstPersonController.Velocity.y);
            }
        }

        private bool OnEnterPlayerState(PlayerState state)
        {
            return firstPersonController.State == state && lastBodyState != state;
        }
    }
}
