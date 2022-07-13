# Character Controller 2D

The Character Controller 2D package by FourthSun contains what the name describes, the missing CharacterController component for your 2D projects.

It behaves (almost) exactly the way the official 3D behaviour so for most users this should be a "plug-and-play" type of behaviour.

## Code Example

```csharp
using FourthSun;
using UnityEngine;

[RequireComponent(typeof(CharacterController2D))]
public class Player : MonoBehaviour {
    [SerializeField]
    [Min(0)]
    private float movementSpeed = 5f;

    private CharacterController2D characterController;

    private void Awake() {
        characterController = GetComponent<CharacterController2D>();
    }
    
    private void Update() {
        float movementInput;
        if (Input.GetKey(KeyCode.RightArrow)) movementInput += 1;
        if (Input.GetKey(KeyCode.LeftArrow)) movementInput -= 1;
        characterController.Move(new Vector2(movementInput * movementSpeed * Time.deltaTime, 0));
        // characterController.IsGrounded
        // characterController.CollisionFlags.HasFlag(CollisionFlags2D.Sides) 
    }
}
```