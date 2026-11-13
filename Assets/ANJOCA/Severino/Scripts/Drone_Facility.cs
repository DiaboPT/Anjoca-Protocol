using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class Drone_Facility : MonoBehaviour
{
    [SerializeField] public GameObject dronePrefab;
    public List<GameObject> drones = new List<GameObject>();
    public int maxDrones = 3;
    private GameObject drone;
    public List<GameObject> patrolPoints = new List<GameObject>();
    public float droneSpawnTimer;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        drone = Instantiate(dronePrefab, patrolPoints[0].transform.position, Quaternion.identity);
        drones.Add(drone);

        EnemyDrone droneScript = drone.GetComponent<EnemyDrone>();

        if (droneScript != null)
        {
            droneScript.walkPoints = patrolPoints.ToArray();
        }
    }

    // Update is called once per frame
    void Update()
    {
        droneSpawnTimer += Time.deltaTime;

        if (droneSpawnTimer >= 60f)
        {
            drones.RemoveAll(d => d == null);
            if (drones.Count < 3)
            {
                drone = Instantiate(dronePrefab, patrolPoints[0].transform.position, Quaternion.identity);
                drones.Add(drone);

                EnemyDrone droneScript = drone.GetComponent<EnemyDrone>();

                if (droneScript != null)
                {
                    droneScript.walkPoints = patrolPoints.ToArray();
                }
                droneSpawnTimer = 0f;
            }
        }
    }
}