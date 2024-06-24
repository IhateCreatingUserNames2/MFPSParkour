using UnityEngine;
using System.Collections;
using MFPS;
using MFPS.InputManager;
using FC_ParkourSystem;

namespace FC_ParkourSystem
{
    public class MFPS_IntegrationHelper : MonoBehaviour, IParkourCharacter
    {
        ParkourController parkourController;
        ClimbController climbController;
        bl_FirstPersonController firstPersonController;
        Animator animator;
        Collider playerCollider;
        bl_PlayerCameraSwitcher cameraSwitcher;
        bl_PlayerAnimations playerAnimations;
        AnimatorHelper animatorHelper;
        bl_PlayerReferences playerReferences;

        public bool UseRootMotion { get; set; } = false;
        public Vector3 MoveDir => firstPersonController.transform.forward * firstPersonController.Velocity.magnitude;
        public bool IsGrounded => firstPersonController.isGrounded;
        public float Gravity => -9.81f;

        public Animator Animator
        {
            get { return animator == null ? GetComponent<Animator>() : animator; }
            set { animator = value; }
        }

        private void Awake()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            Debug.Log("Initializing components in MFPS_IntegrationHelper");

            parkourController = GetComponentInChildren<ParkourController>();
            if (parkourController == null) Debug.LogError("ParkourController is missing.");

            climbController = GetComponentInChildren<ClimbController>();
            if (climbController == null) Debug.LogError("ClimbController is missing.");

            firstPersonController = GetComponent<bl_FirstPersonController>();
            if (firstPersonController == null) Debug.LogError("FirstPersonController is missing.");

            playerCollider = GetComponent<Collider>();
            if (playerCollider == null) Debug.LogError("PlayerCollider is missing.");

            cameraSwitcher = FindObjectOfType<bl_PlayerCameraSwitcher>();
            if (cameraSwitcher == null) Debug.LogError("PlayerCameraSwitcher is missing.");

            playerReferences = GetComponent<bl_PlayerReferences>();
            if (playerReferences == null) Debug.LogError("PlayerReferences is missing.");

            Transform remotePlayer = transform.Find("RemotePlayer");
            if (remotePlayer != null)
            {
                animator = remotePlayer.GetComponentInChildren<Animator>();
                if (animator == null) Debug.LogError("Animator component not found in RemotePlayer.");

                playerAnimations = remotePlayer.GetComponentInChildren<bl_PlayerAnimations>();
                if (playerAnimations == null) Debug.LogError("PlayerAnimations component not found in RemotePlayer.");
            }
            else
            {
                Debug.LogError("RemotePlayer not found under MFPS_IntegrationHelper GameObject.");
            }

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
                   playerCollider == null || animator == null || cameraSwitcher == null || playerAnimations == null || playerReferences == null;
        }

        private void InitializeAnimatorHelper()
        {
            animatorHelper = new AnimatorHelper();
            if (animatorHelper != null)
            {
                animatorHelper.initialize(animator);
            }
        }

        public void OnEndParkourAction()
        {
            if (firstPersonController != null) firstPersonController.enabled = true;
            if (playerCollider != null) playerCollider.enabled = true;
            if (cameraSwitcher != null) cameraSwitcher.enabled = true;
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
        }

        private void Update()
        {
            HandleInputs();
            if (parkourController != null && !parkourController.ControlledByParkour)
            {
                firstPersonController.OnUpdate();
            }
            UpdateCamera();
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
        }

        private void HandleInputs()
        {
            float horizontal = bl_Input.VerticalAxis;
            float vertical = bl_Input.HorizontalAxis;
            bool jump = bl_Input.isButtonDown("Jump");
            bool crouch = bl_Input.isButtonDown("Crouch");
            bool drop = bl_Input.isButtonDown("Drop");
            bool sprint = bl_Input.isButton("Sprint");


            Vector3 move = new Vector3(horizontal, 0, vertical);
            animator.SetFloat("Speed", move.magnitude);

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
                animator.SetTrigger("Jump");
                parkourController.VerticalJump();
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
                if (Input.GetButtonDown("Drop"))
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
                            animator.SetFloat("freeHang", 0);
                            StartCoroutine(climbController.JumpToLedge(currentPoint.transform, "DropToHang", 0.50f, 0.90f, rotateToLedge: true, matchStart: AvatarTarget.LeftFoot));
                        }
                        else
                        {
                            animator.SetFloat("freeHang", 1);
                            StartCoroutine(climbController.JumpToLedge(currentPoint.transform, "DropToFreeHang", 0.50f, 0.89f, rotateToLedge: true, matchStart: AvatarTarget.LeftFoot));
                        }
                    }
                }
            }
        }

        public void UpdateCamera()
        {
            // Handle camera updates here based on your specific requirements.
            // The implementation can vary based on how you want the camera to behave during parkour actions.
        }

        public IEnumerator HandleVerticalJump()
        {
            if (firstPersonController != null && firstPersonController.isGrounded)
            {
                firstPersonController.DoJump();
            }
            yield break;
        }

        public bool PreventParkourAction => firstPersonController != null &&
                                            (firstPersonController.State == PlayerState.Crouching || firstPersonController.State == PlayerState.Running);
    }
}
