using UnityEngine;
using UnityEngine.Events;

public class PlayerMovement : MonoBehaviour {
    [SerializeField] private float m_Speed = 5f;
    [SerializeField] private float m_JumpForce = 400f;                          // Amount of force added when the player jumps.
    [SerializeField] private int m_JumpBuffer = 5;
    [SerializeField] private int m_GroundedBuffer = 5;
    [Range(0, .3f)] [SerializeField] private float m_MovementSmoothing = .05f;  // How much to smooth out the movement
    [SerializeField] private bool m_AirControl = false;                         // Whether or not a player can steer while jumping;
    [SerializeField] private bool m_DoubleJump = false;
    [SerializeField] private LayerMask m_WhatIsGround;                          // A mask determining what is ground to the character
    [SerializeField] private LayerMask m_WhatIsInvGround;
    [SerializeField] private Transform m_GroundCheck;                           // A position marking where to check if the player is grounded.
    [SerializeField] private RiftManager m_Rift;
    public float maxSpeed = 10;
    public ChaseCam chase;

    const float k_GroundedRadius = .2f; // Radius of the overlap circle to determine if grounded
    private bool m_Grounded;            // Whether or not the player is grounded.
    private bool rifting = false;
    private bool canDoubleJump;
    private Rigidbody2D m_Rigidbody2D;
    private bool m_FacingRight = true;  // For determining which way the player is currently facing.
    private Vector3 m_Velocity = Vector3.zero;
    private bool canJump = true;
    private int lastJump = 0;
    private bool frozen = false;
    private int cantJumpCounter = 0;
    private int lastGrounded = 0;
    private bool wasFollowing = false;

    private Vector2 respawnPoint;

    private Animator animator;
    private GameObject murderer;

    private void Awake() {
        m_Rigidbody2D = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    private void FixedUpdate() {
        rifting = m_Rift.GetRifting();
        
        bool wasGrounded = m_Grounded;
        m_Grounded = false;

        // The player is grounded if a circlecast to the groundcheck position hits anything designated as ground
        // This can be done using layers instead but Sample Assets will not overwrite your project settings.
        bool follow = false;
        Collider2D[] colliders = Physics2D.OverlapCircleAll(m_GroundCheck.position, k_GroundedRadius, m_WhatIsGround);
        for (int i = 0; i < colliders.Length; i++) {
            if (colliders[i].gameObject != gameObject) {
                lastGrounded = 0;
                follow = colliders[i].gameObject.layer == LayerMask.NameToLayer("Invisible Environment") && !rifting;
                break;
            }
        }
        if (lastGrounded < m_GroundedBuffer) {
            lastGrounded++;
            m_Grounded = true;
            canDoubleJump = true;
        }
        if (follow && !wasFollowing) m_Rift.Follow();
        else if (!follow && wasFollowing) m_Rift.StopFollowing();
        wasFollowing = follow;

        // Disable jumping immediately after jumping to avoid super fast double jump
        if (!canJump) {
            cantJumpCounter++;
            if (cantJumpCounter > 5) canJump = true;
        }
    }


    public void Move(float move, bool jump) {
        if (frozen) {
            if (m_Grounded) m_Rigidbody2D.velocity = new Vector2(0, m_Rigidbody2D.velocity.y);
            return;
        }

        // Jump buffer stuff
        if (lastJump == m_JumpBuffer && !jump) lastJump++;

        if (jump && lastJump > m_JumpBuffer) lastJump = 0;
        else jump = false;

        if (lastJump < m_JumpBuffer) {
            lastJump++;
            jump = true;
        }
        

        //only control the player if grounded or airControl is turned on
        if (m_Grounded || m_AirControl) {
            float targetX = move * m_Speed, temp = 0;
            float xVelocity = Mathf.SmoothDamp(m_Rigidbody2D.velocity.x, targetX, ref temp, m_MovementSmoothing);

            m_Rigidbody2D.velocity = new Vector2(xVelocity, m_Rigidbody2D.velocity.y);

            // If the input is moving the player right and the player is facing left...
            if (move > 0 && !m_FacingRight) {
                // ... flip the player.
                Flip();
            }
            // Otherwise if the input is moving the player left and the player is facing right...
            else if (move < 0 && m_FacingRight) {
                // ... flip the player.
                Flip();
            }
        }

        bool readyToJump = (m_Grounded || (m_DoubleJump && canDoubleJump)) && canJump;
        // bool flare = (!m_Grounded && (m_DoubleJump && canDoubleJump && rifting)) && canJump && jump;
        // if (flare) m_Rift.Flare();
        // If the player should jump...
        if (readyToJump && jump) {
            // Stop their current vertical velocity
            m_Rigidbody2D.velocity = new Vector2(m_Rigidbody2D.velocity.x, 0);

            // Add new vertical force to player
            m_Rigidbody2D.AddForce(new Vector2(0f, m_JumpForce));

            if(!m_Grounded) OpenRift();
            canJump = false;
            cantJumpCounter = 0;
        }

        if (m_Rigidbody2D.velocity.y < -maxSpeed) {
            m_Rigidbody2D.velocity = new Vector2(m_Rigidbody2D.velocity.x, -maxSpeed);
        }

        // Animator updates
        animator.SetBool("Grounded", m_Grounded);
        animator.SetFloat("Speed", Mathf.Abs(move));
        animator.SetFloat("vSpeed", m_Rigidbody2D.velocity.y);
    }

    public void SetRespawn(Vector2 point) {
        respawnPoint = point;
    }

    public void Die(GameObject other=null) {
        if (!frozen) {
            animator.SetBool("Die", true);
            frozen = true;
            m_Rift.Grow();
            murderer = other;
            GetComponent<AudioSource>().PlayDelayed(0);
        }
    }

    public void Respawn() {
        if (chase != null) chase.ResetChase();

        m_Rift.Reset();
        animator.SetBool("Die", false);
        animator.Play("Idle", -1, 0f);
        transform.position = respawnPoint;
        m_Rigidbody2D.velocity = Vector2.zero;
        canDoubleJump = false;
        frozen = false;
        if (murderer != null) murderer.GetComponent<BlobController>().TeleportToEdge();
    }

    public void OpenRift() {
        canDoubleJump = false;
        m_Rift.OpenRift(transform.position);
    }

    public void CloseRift() {
        if (rifting) {
            m_Rift.CloseRift();
        }
    }

    private void Flip() {
        // Switch the way the player is labelled as facing.
        m_FacingRight = !m_FacingRight;

        // Multiply the player's x local scale by -1.
        Vector3 theScale = transform.localScale;
        theScale.x *= -1;
        transform.localScale = theScale;

        m_Rift.SetFlip(m_FacingRight);
    }

    public void SetFrozen(int val) {
        frozen = val != 0;
    }

    public bool GetFrozen() {
        return frozen;
    }
}
