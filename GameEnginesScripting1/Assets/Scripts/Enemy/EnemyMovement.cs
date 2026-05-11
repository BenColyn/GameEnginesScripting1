using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    [SerializeField] private float speed = -1.0f;
    [SerializeField] private float maxDistance = 5.0f;
    public GameObject target;
    public Transform targetPosition;
    private bool inRange = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {   
        transform.LookAt(targetPosition);
        if (target != null)
        {
            Vector3 direction = (target.transform.position - transform.position);
            if (direction.magnitude <= maxDistance)
            {
                inRange = true;
               
            }
            if (direction.magnitude >= maxDistance)
            {
               inRange=false;
                direction = direction.normalized;
                transform.Translate(direction * speed * Time.deltaTime, Space.World);
              

            }
           
            
        }
    }
    public void SetTatget(GameObject inTarget)
    {
        this.target = inTarget;
    }
    public void SetSpeed(float inSpeed)
    {
        this.speed = inSpeed;
    }
    public void SetTargetPosition(Transform inTarget)
    {
        this.targetPosition = inTarget;
    }
    public bool GetInRange() 
    {
        return inRange;
    }
}
