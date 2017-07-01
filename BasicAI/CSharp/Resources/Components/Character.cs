using System;
using AtomicEngine;

public class Character : CSComponent
{
    void Start()
    {
        SubscribeToEvent("NodeCollision", HandleNodeCollision);
		SubscribeToEvent<KeyDownEvent>(e =>
		{
			if (e.Key == Constants.KEY_ESCAPE)
				GetSubsystem<Engine>().Exit();
		});

        Node objectNode = Node;
        AnimatedModel animatedModel;

		if (objectNode.GetComponent<AnimatedModel>() == null)
		{
			animatedModel = objectNode.CreateComponent<AnimatedModel>();
			animatedModel.Model = GetSubsystem<ResourceCache>().GetResource<Model>("Models/Jack.mdl");
		}
		else
		{
			animatedModel = objectNode.GetComponent<AnimatedModel>();
		}

		// Create animation controller
		objectNode.CreateComponent<AnimationController>();

		// Create rigidbody, and set non-zero mass so that the body becomes dynamic
		RigidBody body = objectNode.CreateComponent<RigidBody>();
		body.CollisionLayer = 1;
		body.Mass = 1.0f;

		// Set zero angular factor so that physics doesn't turn the character on its own.
		// Instead we will control the character yaw manually
		body.AngularFactor = Vector3.Zero;

		// Set the rigidbody to signal collision also when in rest, so that we get ground collisions properly
		body.CollisionEventMode = CollisionEventMode.COLLISION_ALWAYS;

		// Set a capsule shape for collision
		CollisionShape shape = objectNode.CreateComponent<CollisionShape>();
		shape.SetCapsule(1.5f, 1.8f, new Vector3(0.0f, 0.9f, 0.0f));

		weapon = objectNode.CreateComponent<WeaponCrateGun>();

		AtomicNET.GetSubsystem<Renderer>().ShadowMapSize = 2048;

		// Get Camera from Scene
		Vector<Node> children = new Vector<Node>();
		Scene.GetChildrenWithComponent<Camera>(children, true);
		if (children.Size > 0)
		{
			cameraNode = children[0];
		}
		cameraNode.GetComponent<Camera>().Fov = 65;
    }

	void Update(float timeStep)
	{
		var input = GetSubsystem<Input>();

		input.SetMouseVisible(false);

		yaw += (float)input.MouseMoveX * PITCH_SENSITIVITY;
		pitch += (float)input.MouseMoveY * YAW_SENSITIVITY;

		// Limit pitch
		pitch = Clamp<float>(pitch, -90.0f, 90.0f);

		// Set rotation already here so that it's updated every rendering frame instead of every physics frame
		Node.Rotation = Quaternion.FromAxisAngle(Vector3.Up, yaw);

		if (input.GetMouseButtonPress(Constants.MOUSEB_LEFT))
		{
			weapon.fireProjectile(cameraNode);
		}
	}

	void PostUpdate(float timeStep)
	{
		if (cameraNode == null)
			return;

		Node characterNode = Node;

		// Get camera lookat dir from character yaw + pitch
		Quaternion rot = characterNode.Rotation;

		Quaternion dir = rot * Quaternion.FromAxisAngle(Vector3.Right, pitch);

		// Turn head to camera pitch, but limit to avoid unnatural animation

		Node headNode = characterNode.GetChild("Bip01_Head", true);

		float limitPitch = Clamp<float>(pitch, -45.0f, 45.0f);
		Quaternion headDir = rot * Quaternion.FromAxisAngle(new Vector3(1.0f, 0.0f, 0.0f), limitPitch);

		// This could be expanded to look at an arbitrary target, now just look at a point in front
		Vector3 headWorldTarget = headNode.WorldPosition + headDir * (new Vector3(0.0f, 0.0f, -1.0f));
		headNode.LookAt(headWorldTarget, new Vector3(0.0f, 1.0f, 0.0f));

		cameraNode.Position = headNode.WorldPosition + rot * (new Vector3(0.0f, 0.15f, 0.2f));
		cameraNode.Rotation = dir;
	}

    void PhysicsPreStep(float timeStep)
    {
        /// TODO: Could cache the components for faster access instead of finding them each frame
        /// Also, switch to generic version of GetComponent
        /// 
        RigidBody body = (RigidBody)Node.GetComponent("RigidBody");
        AnimationController animCtrl = (AnimationController)Node.GetComponent("AnimationController", true);

        // Update the in air timer. Reset if grounded
        if (!onGround)
            inAirTimer += timeStep;
        else
            inAirTimer = 0.0f;

        // When character has been in air less than 1/10 second, it's still interpreted as being on ground
        bool softGrounded = inAirTimer < INAIR_THRESHOLD_TIME;

        // Update movement & animation
        Quaternion rot = Node.Rotation;

        Vector3 moveDir = Vector3.Zero;

        Vector3 velocity = body.LinearVelocity;

        // Velocity on the XZ plane

        Vector3 planeVelocity = new Vector3(velocity.X, 0.0f, velocity.Z);

        var input = GetSubsystem<Input>();

        if (input.GetKeyDown(Constants.KEY_W))
        {
            moveDir += Vector3.Forward;
        }

        if (input.GetKeyDown(Constants.KEY_S))
        {
            moveDir += Vector3.Back;
        }

        if (input.GetKeyDown(Constants.KEY_A))
        {
            moveDir += Vector3.Left;
        }

        if (input.GetKeyDown(Constants.KEY_D))
        {
            moveDir += Vector3.Right;
        }

        float breakAdjust = ((float)input.MouseMoveWheel) * timeStep * 4.0f;
        breakForce -= breakAdjust;
        breakForce = Clamp<float>(breakForce, MIN_BRAKE_FORCE, MAX_BRAKE_FORCE);

        // Normalize move vector so that diagonal strafing is not faster
        if (moveDir.LengthSquared > 0.0f)
            moveDir.Normalize();

        // If in air, allow control, but slower than when on ground
        if(input.GetKeyDown(Constants.KEY_SHIFT)){
            body.ApplyImpulse(rot * moveDir * (softGrounded ? SPRINT_MOVE_FORCE : INAIR_MOVE_FORCE));
        } else {
            body.ApplyImpulse(rot * moveDir * (softGrounded ? MOVE_FORCE : INAIR_MOVE_FORCE));
        }



        if (softGrounded)
        {
            // When on ground, apply a braking force to limit maximum ground velocity
            Vector3 brakeForce = -planeVelocity * breakForce;
            body.ApplyImpulse(brakeForce);

            // Jump. Must release jump control inbetween jumps
            if (input.GetKeyDown(Constants.KEY_SPACE))
            {
                if (okToJump)
                {
                    body.ApplyImpulse(Vector3.Up * JUMP_FORCE);
                    okToJump = false;
                }
            }
            else
                okToJump = true;
        }

        if (onGround)
        {
            // Play walk animation if moving on ground, otherwise fade it out
            if (softGrounded && !moveDir.Equals(Vector3.Zero))
                animCtrl.PlayExclusive("Models/Jack_Walk.ani", 0, true, 0.2f);

            // Set walk animation speed proportional to velocity
            animCtrl.SetSpeed("Models/Jack_Walk.ani", planeVelocity.Length * 0.3f);
        }

        // Reset grounded flag for next frame
        onGround = false;

    }

    void HandleNodeCollision(uint eventType, ScriptVariantMap eventData)
    {
        PhysicsNodeCollision nodeCollision = eventData.GetPtr<PhysicsNodeCollision>("PhysicsNodeCollision");

        if (nodeCollision == null)
            return;

        // TODO: We need to be able to subscribe to specific object's events
        if (nodeCollision.OtherNode == Node)
        {
            var nodePosition = Node.Position;

            for (uint i = 0; i < nodeCollision.Contacts.Size; i++)
            {
                var contact = nodeCollision.Contacts.At(i);

                var contactPosition = contact.Position;

                // TODO: This needs to be flipped when can listen to specific node

                // If contact is below node center and pointing up, assume it's a ground contact
                if (contactPosition.Y > (nodePosition.Y - 1.0f))
                {
                    float level = contact.Normal.Y;
                    
                    if (level < 0.75f)
                        onGround = true;
                }

            }

        }

    }

    const float MOVE_FORCE = 0.8f;
    const float SPRINT_MOVE_FORCE = 1.5f;
    const float INAIR_MOVE_FORCE = 0.02f;
    const float JUMP_FORCE = 7.0f;
    const float YAW_SENSITIVITY = 0.1f;
	const float PITCH_SENSITIVITY = 0.1f;
    const float INAIR_THRESHOLD_TIME = 0.1f;

    const float MIN_BRAKE_FORCE = 0.1f;
    const float MAX_BRAKE_FORCE = 0.4f;


    float breakForce = 0.2f;

	Node cameraNode;
	Weapon weapon;
	float pitch = 0.0f;
	float yaw = 0.0f;

    /// Grounded flag for movement.
    bool onGround = false;
    /// Jump flag.
    bool okToJump = true;
    /// In air timer. Due to possible physics inaccuracy, character can be off ground for max. 1/10 second and still be allowed to move.
    float inAirTimer = 0.0f;

	public float LightValue { get; set; }

    // TODO: add this to a utility class
    static T Clamp<T>(T val, T min, T max) where T : IComparable<T>
    {
        if (val.CompareTo(min) < 0) return min;
        else if (val.CompareTo(max) > 0) return max;
        else return val;
    }
}
