using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.AI;

public class PlayerMovement : MonoBehaviour {

    public Animator animator;
    public NavMeshAgent agent;
    public float inputHoldDelay = .5f;
    public float TurnSpeedThreshold = .5f;
    public float speedDampTime = .1f;
    public float slowingSpeed = .175f;
    public float turnSmoothing = 15f;

    private WaitForSeconds _inputHoldWait;
    private Vector3 _destinationPosition;
    private Interactable _currentInteractable;
    private bool _handleInput = true;

    private const float _STOP_DISTANCE_PROPORTION = 0.1f;
    private const float _NAVMESH_SAMPLE_DISTANCE = 4f;

    private readonly int _hashSpeedPara = Animator.StringToHash("Speed");
    private readonly int _hashLocomotionTag = Animator.StringToHash("Locomotion");

    // Take control of rotation from agent, initialize input delay, reset agent destination
    private void Start()
    {
        agent.updateRotation = false;
        _inputHoldWait = new WaitForSeconds(inputHoldDelay);
        _destinationPosition = transform.position;
    }

    private void OnAnimatorMove()
    {
        agent.velocity = animator.deltaPosition / Time.deltaTime;
    }

    private void Update()
    {
        // We havn't calculated a path yet
        if(agent.pathPending)
        {
            return;
        }

        // Get a reference to the speed of the agent
        float speed = agent.desiredVelocity.magnitude;

        // Agent is within stopping distance
        if(agent.remainingDistance <= agent.stoppingDistance * _STOP_DISTANCE_PROPORTION)
        {
            // Get new speed
            Stopping(out speed);
        }
        // Agent is within slowing distance
        else if(agent.remainingDistance <= agent.stoppingDistance)
        {
            // Get new speed
            Slowing(out speed, agent.remainingDistance);
        }
        // Keep moving to target destination
        else if(speed > TurnSpeedThreshold)
        {
            Moving();
        }

        // Animate the character using speed
        animator.SetFloat(_hashSpeedPara, speed, speedDampTime, Time.deltaTime);
    }

    // Stop the agent, set the destination to transform, output 0f speed
    private void Stopping(out float speed)
    {
        agent.isStopped = true;
        transform.position = _destinationPosition;
        speed = 0f;

        if(_currentInteractable)
        {
            transform.rotation = _currentInteractable.interactionLocation.rotation;
            _currentInteractable.Interact();
            _currentInteractable = null;
            StartCoroutine(WaitForInteraction());
        }
    }

    // Take control of position from agent, move the agent to position, update speed
    private void Slowing(out float speed, float distanceToDestination)
    {
        agent.isStopped = true;
        transform.position = Vector3.MoveTowards(transform.position, _destinationPosition, slowingSpeed * Time.deltaTime);
        float proportionalDistance = 1f - distanceToDestination / agent.stoppingDistance;
        speed = Mathf.Lerp(slowingSpeed, 0, proportionalDistance);

        // If player is going to interactable, lerp to rotational normal of that interactable
        Quaternion targetRotation = _currentInteractable ? _currentInteractable.interactionLocation.rotation :
                                                           transform.rotation;
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, proportionalDistance);
    }
    
    // Update rotation of agent while moving
    private void Moving()
    {
        Quaternion targetRotation = Quaternion.LookRotation(agent.desiredVelocity);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, turnSmoothing * Time.deltaTime);
    }

    // Raycast a screen position to nearest world position on NavMesh, set agent destination, move agent
    public void OnGroundClick(BaseEventData data)
    {
        if(!_handleInput)
        {
            return;
        }

        _currentInteractable = null;

        PointerEventData pData = (PointerEventData)data;
        NavMeshHit hit;
        if(NavMesh.SamplePosition(pData.pointerCurrentRaycast.worldPosition, out hit, _NAVMESH_SAMPLE_DISTANCE, NavMesh.AllAreas))
        {
            _destinationPosition = hit.position;
        } 
        else
        {
            _destinationPosition = pData.pointerCurrentRaycast.worldPosition; 
        }

        agent.SetDestination(_destinationPosition);
        agent.isStopped = false;
    }


    // Handle clicks of Interactable, set agent destination, move agent
    public void OnInteractableClick(Interactable interactable)
    {
        if(!_handleInput)
        {
            return;
        }

        _currentInteractable = interactable;
        _destinationPosition = _currentInteractable.interactionLocation.position;
        agent.SetDestination(_destinationPosition);
        agent.isStopped = false;
    }

    private IEnumerator WaitForInteraction()
    {
        _handleInput = false;
        yield return _inputHoldWait;
        while(animator.GetCurrentAnimatorStateInfo(0).tagHash != _hashLocomotionTag)
        {
            yield return null;
        }
        _handleInput = true;
    }
}
