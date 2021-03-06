using UnityEngine;

public class RiftManager : MonoBehaviour {
    public GameObject environment;
    public AudioSource music, fx;
    public float radiusOffset = 3;

    [HideInInspector]
    public float radius;
    private float lastRadius;
    public Transform follow;
    public float outOfRange = 40;
    public bool fromStart = false;

    private Animator animator;
    private CircleCollider2D trigger;
    private bool following = false;
    private Vector2 lastPos;
    private RiftParameters[] shaders;
    private BlobController[] blobs;

    private bool rifting = false;
    private bool wasPlayerInRift = false;


    void Start() {
        animator = GetComponent<Animator>();
        trigger = GetComponent<CircleCollider2D>();

        shaders = environment.GetComponentsInChildren<RiftParameters>(true);
        lastRadius = radius = 0;

        blobs = environment.GetComponentsInChildren<BlobController>();

        if (!fromStart) SetRiftRadius(0);
        else {
            SetRiftRadius(12);
            SetRiftOrigin(transform.position);
        }
    }

    void Update() {
        if (fromStart) return;

        if (lastRadius != radius) {
            SetRiftRadius(radius);
            lastRadius = radius;
            if (radius > radiusOffset) trigger.radius = radius - radiusOffset;
        }

        if (following && lastPos != (Vector2) follow.position) {
            SetRiftOrigin(follow.position);
            lastPos = (Vector2) follow.position;
        }

        if (Vector2.Distance(follow.position, transform.position) > outOfRange && rifting) {
            CloseRift();
        }
    }

    public void Flare() {
        animator.SetTrigger("Flare");
    }

    public void ResetFlare() {
        animator.ResetTrigger("Flare");
    }

    public void Grow() {
        if (radius == 0) SetRiftOrigin(follow.position);
        animator.SetTrigger("Grow");
    }

    public void Reset() {
        rifting = false;
        following = false;
        trigger.radius = 0;
        radius = 0;
        StopRift();
        SetRiftRadius(0);
        animator.SetInteger("Open", 1);
        animator.ResetTrigger("Grow");
        animator.Play("Idle", -1, 0);
    }

    public void SetFollowing(int val) {
        following = val > 0;
        if (following) {
            SetRiftOrigin(follow.position);
            lastPos = (Vector2) follow.position;
        } 
    }

    public void Follow() {
        if (!following && !rifting) {
            animator.SetInteger("Open", 2);
            following = true;
        }
    }

    public void StopFollowing() {
        if (following) {
            CloseRift();
        }
    }

    public void SetFlip(bool val) {
        GetComponent<SpriteRenderer>().flipX = val;
    }

    public void OpenRift(Vector2 origin) {
        if (!rifting) {
            SetRiftOrigin(origin);
            animator.SetInteger("Open", 3);
        }
    }

    public void CloseRift() {
        if(animator.GetInteger("Open") != 1) {
            animator.SetInteger("Open", 1);
        }
    }

    public void StartRift() {
        fx.Play();
        rifting = true;
        RiftNotifyBlobs(rifting);
        PlayerNotifyBlobs(true);
    }

    public void StopRift() {
        fx.Pause();
        rifting = false;
        RiftNotifyBlobs(rifting);
    }

    public bool GetRifting() {
        return rifting;
    }

    private void OnTriggerStay2D(Collider2D other) {
        if (radius > radiusOffset && other.CompareTag("Player")) {
            if (!wasPlayerInRift) {
                PlayerNotifyBlobs(true);
                wasPlayerInRift = true;
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other) {
        if (other.CompareTag("Player")) {
            PlayerNotifyBlobs(false);
            wasPlayerInRift = false;
        }
    }

    private void SetRiftRadius(float rad) {
        foreach (RiftParameters shader in shaders) {
            if (shader.gameObject.activeInHierarchy) shader.UpdateRadius(rad);
        }
    }

    private void SetRiftOrigin(Vector2 origin) {
        transform.position = origin;
        foreach (RiftParameters shader in shaders) {
             if (shader.gameObject.activeInHierarchy) shader.UpdateOrigin(origin);
        }
    }

    private void RiftNotifyBlobs(bool start) {
        if (start) {
            foreach (var blob in blobs) {
                blob.OnRiftOpen(transform.position);
            }
        } else {
            foreach (var blob in blobs) {
                blob.OnRiftClose();
            }
        }
    }

    private void PlayerNotifyBlobs(bool enter) {
        if (enter) {
            foreach (var blob in blobs) {
                blob.OnPlayerEnterRift();
            }
        } else {
            foreach (var blob in blobs) {
                blob.OnPlayerExitRift();
            }
        }
    }
}
