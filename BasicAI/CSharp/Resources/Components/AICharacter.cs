using System;
using System.Collections.Generic;
using AtomicEngine;
using System.Reflection;

public class AICharacter : CSComponent
{
    public void Start()
    {
        SubscribeToEvent("NodeCollision", HandleNodeCollision);

		Node objectNode = this.GetNode();
		AnimatedModel animatedModel;

		// Create the rendering component + animation controller
		// Do we have a model already? If not, add the default
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
		shape.SetCapsule(1.0f, 1.8f, new Vector3(0.0f, 0.9f, 0.0f));

        objectNode.CreateComponent<Ragdoll>();

        //Do we have a path?
        if (path != null)
        {
            Node currPath = Scene.GetChild(path);
            //Start of the path
            currPoint = 0;
            Vector<Node> points = currPath.GetChildren();
            currentPath = currPath.GetChild(0).WorldPosition;
        }
        else if (isWandering == true)
        {
            //If not, Get a random position to wander to
            Random rnd = new Random();
            Vector3 newLoc = new Vector3(rnd.Next(-5, 5) * (float)rnd.NextDouble(), 0, rnd.Next(-5, 5) * (float)rnd.NextDouble());
            currentPath = Vector3.Add(objectNode.WorldPosition, newLoc);
        }
        else
        {
            //I guess we just stand heRE LIKE ANIMALS
        }
        fireCount = 0;
        isAlive = true;
    }

    public void Update(float timeStep){
        //We don't want to fire every frame - that would be insane.
        if (fireCount > 0)
        {
            fireCount--;
        }

        //If we're set to wandering, wander the wild yonder!
        if(isWandering){
            FollowPath(timeStep);
        }

        //Get the player component
        Vector<Character> Player = new Vector<Character>();
        Scene.GetChild("Player").GetDerivedCSComponents<Character>(Player);

        // If we're hostile, follow the player and shoot if they're in our FoV
        if (isHostile)
        {
            // First, we check if the player is in their FoV
            float dirToPlayer = Check2DAngletoTarget(Player[0].Node);
            if (dirToPlayer < Convert.ToSingle(fov) / 2 && isAlive)
            {
				// If we are, THEN we raycast. I think it's cheaper this way
				Octree octree = Scene.GetComponent<Octree>();
                Ray ray = new Ray(new Vector3(Node.WorldPosition.X, 0.9f, Node.WorldPosition.Z), Node.WorldDirection);
				RayOctreeQuery query = new RayOctreeQuery(ray, RayQueryLevel.RAY_TRIANGLE, 20, 1, 1);
				octree.Raycast(query);

                // The AIPlayer should be result 0, so the next object in line will be the first collision.
                // Go through the results until we come across something we want to hit.
                // Need to find out if there's a better way to do this. We need to ignore ourselves and Projectiles.
                // (Xamarin wants me to initialise this - Not sure if that's a C# thing or an IDE/Compiler thing)
                Node Hit = null;

                for (int i = 0; i < query.Results.Count; i++)
                {
                    // We can't just search for "Player" in case there's a huge frickin' wall or something in the way
                    if (query.Results[i].Node.Name != "AICharacter" && !query.Results[i].Node.Name.Contains("projectile")){
                        if(query.Results[i].Node != null){
                            Hit = query.Results[i].Node;
                            break;
                        }
                    }
                }

                //If we hit something, do something with it!
                if (Hit != null)
                {
                    if(Hit.Name == "Player"){
                        isChasing = true;
                        currentPath = new Vector3(Hit.Position.X, 0, Hit.Position.Z);
						Vector<Weapon> weapons = new Vector<Weapon>();
						Node.GetDerivedCSComponents<Weapon>(weapons, true);
                        if (weapons.Count > 0)
						{
							if (fireCount == 0)
							{
								weapons[0].fireProjectile(Node.GetChild("Bip01_Head", true));
								fireCount = 50;
							}
						}
                    } else {
                        isChasing = false;
                    }
                }
            }
        }
    }

    void PhysicsPreStep(float timeStep)
    {
        /// TODO: Could cache the components for faster access instead of finding them each frame
        /// Also, switch to generic version of GetComponent

        if ((RigidBody)Node.GetComponent("RigidBody") != null)
        {
            /// TODO: Could cache the components for faster access instead of finding them each frame
            /// Also, switch to generic version of GetComponent
            /// 
            RigidBody body = (RigidBody)Node.GetComponent("RigidBody");
            AnimationController animCtrl = (AnimationController)Node.GetComponent("AnimationController", true);

            // Update movement & animation
            Quaternion rot = Node.Rotation;

            Vector3 planeVelocity = new Vector3(body.LinearVelocity.X, 0.0f, body.LinearVelocity.Z);

            if (isMoving)
            {
                body.LinearVelocity = rot * Vector3.Forward * MOVE_FORCE;
            }

            // Play walk animation if moving on ground, otherwise fade it out
            if (!moveDir.Equals(Vector3.Zero))
                animCtrl.PlayExclusive("Models/Jack_Walk.ani", 0, true, 0.2f);

            // Set walk animation speed proportional to velocity
            animCtrl.SetSpeed("Models/Jack_Walk.ani", planeVelocity.Length * 0.3f);
        } else {
            //We must be in ragdoll; ergo; we have passed
            isAlive = false;
        }
    }

    void HandleNodeCollision(uint eventType, ScriptVariantMap eventData)
    {
        PhysicsNodeCollision nodeCollision = eventData.GetPtr<PhysicsNodeCollision>("PhysicsNodeCollision");

        if (nodeCollision == null)
            return;

        // TODO: We need to be able to subscribe to specific object's events
        if (nodeCollision.OtherNode == Node)
        {

        }

    }

    void TurnToFace(float timeStep, Vector3 direction)
    {
        Vector3 worldSpaceTarget = direction;
		Vector3 lookDir = worldSpaceTarget - Node.Position;
        // Check if target is very close, in that case can not reliably calculate lookat direction
        if (!lookDir.Equals(Vector3.Zero) && (RigidBody)Node.GetComponent("RigidBody") != null)
        {
			Quaternion newRotation = new Quaternion();
            // Do nothing if setting look rotation failed
            if (Quaternion.FromLookRotation(lookDir, Vector3.UnitY, out newRotation))
            {
                //Get our FPS, or, set to 60 if none
                uint frameRate = 60;
                if (GetSubsystem<Engine>().Fps > 0)
                {
                    frameRate = GetSubsystem<Engine>().Fps;
                }

                float angle = Node.Rotation.ToEulerAngles().Y - newRotation.ToEulerAngles().Y;

                if (angle < 0)
                    angle = 360 + angle;

                if (angle < 180 && angle > 1)
                {
                    Node.Yaw(timeStep * frameRate * TURN_SPEED);
                }
                else if (angle > 180 && angle < 359)
                {
                    Node.Yaw(-timeStep * frameRate * TURN_SPEED);
                }
            }
        }
    }

	void FollowPath(float timeStep)
	{
		if (currentPath != null)
		{
			Random rnd = new Random();
			DebugRenderer dbgRend = Scene.GetComponent<DebugRenderer>();
			dbgRend.AddCross(currentPath, 1.0f, Color.White, false);

			Vector3 nextWaypoint = currentPath; // NB: currentPath[0] is the next waypoint in order

			// Rotate Jack toward next waypoint to reach and move.
            // Because we're gravity-bound, we only wnat to make sure we have a 2D psoition check.
            // This is so the character isn't standing underneith a waypoint and stuck rotating.
            Vector2 nodePosition2D = new Vector2(Node.Position.X, Node.Position.Z);
            Vector2 waypointPosition2D = new Vector2(nextWaypoint.X, nextWaypoint.Z);
			float distance = (nodePosition2D - waypointPosition2D).Length;

			// Look at the next node
			TurnToFace(timeStep, new Vector3(nextWaypoint.X, Node.WorldPosition.Y, nextWaypoint.Z));
			// We don't know if they're using a Biped
			if(Node.GetChild("Bip01_Head", true) != null)
				Node.GetChild("Bip01_Head", true).LookAt(nextWaypoint, Vector3.UnitY, TransformSpace.TS_WORLD);

			//Move the character toward it
			this.moveDir = Vector3.Forward;
            isMoving = true;

            if (distance < 1.0f && !isChasing)
			{
                //Are we on a path?
                if(path != null)
                {
					Node currPath = Scene.GetChild(path);
					//Start of the path
					Vector<Node> points = currPath.GetChildren();
                    if(currPoint == (int)points.Size - 1)
                    {
                        currPoint = 0;
                    }
                    else
                    {
                        currPoint++;
                    }
                    currentPath = currPath.GetChild(Convert.ToString(currPoint)).WorldPosition;
                }
                else if(isWandering)
                {
					Vector3 newLoc = new Vector3(rnd.Next(-5, 5) * (float)rnd.NextDouble(), 0, rnd.Next(-5, 5) * (float)rnd.NextDouble());
					currentPath = Vector3.Add(Node.WorldPosition, newLoc);
                }
			}
		}
	}

    float Check2DAngletoTarget(Node targetNode){
        Vector3 Dir = Node.GetDirection();
        Dir.Normalize();
        Vector3 relativePos = Vector3.Subtract(targetNode.Position, Node.Position);
        float angle = Convert.ToSingle(Vector3.CalculateAngle(relativePos, Dir) * (180 / Math.PI));
        if(angle < 0){
            angle = 360 + angle;
        }
        return angle;
    }

    const float MOVE_FORCE = 1.4f;
    const float SPRINT_MOVE_FORCE = 1.5f;
    const float TURN_SPEED = 2.0f;
    int fireCount;
    bool isAlive;
    public Vector3 moveDir { get; set; }
    Vector3 currentPath = new Vector3();

    int currPoint;

    bool isMoving = false;
    bool isChasing = false;
    // TODO: add this to a utility class
    static T Clamp<T>(T val, T min, T max) where T : IComparable<T>
    {
        if (val.CompareTo(min) < 0) return min;
        else if (val.CompareTo(max) > 0) return max;
        else return val;
    }

    [Inspector]
    bool isWandering = true;

    [Inspector]
    bool isHostile = true;

    [Inspector]
    string fov = "90";

    [Inspector]
    string path = null;
}