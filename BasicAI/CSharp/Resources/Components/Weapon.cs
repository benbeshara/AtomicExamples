using System;
using AtomicEngine;

public class Weapon : CSComponent
{
	public void fireProjectile(Node cameraNode)
	{
        projectile.SpawnProjectile(cameraNode);
	}

    public Projectile projectile { get; set; }
}

public class WeaponCrateGun : Weapon
{
    public void Start()
    {
        this.projectile = Scene.CreateComponent<ProjectileBullet>();
    }
}

public class Projectile : CSComponent
{
    public string modelName { get; set; }
    public string impactType { get; set; }
    Node projNode;

    public void SpawnProjectile(Node cameraNode)
    {
	    projNode = Scene.CreateChild("projectile");
        SubscribeToEvent<PhysicsCollisionEvent>(OnCollision);
		StaticModel model = projNode.CreateComponent<StaticModel>();
		RigidBody body = projNode.CreateComponent<RigidBody>();
		CollisionShape shape = projNode.CreateComponent<CollisionShape>();
		model.Model = GetSubsystem<ResourceCache>().GetResource<Model>(modelName);

        projNode.Scale = new Vector3(0.01f, 0.01f, 0.01f);
        projNode.Rotation = cameraNode.WorldRotation;
		body.Mass = 10.0f;
		body.CollisionLayer = 1;
		body.CollisionEventMode = CollisionEventMode.COLLISION_ALWAYS;

		shape.SetBox(new Vector3(0.01f, 0.01f, 0.01f));

		projNode.Position = cameraNode.WorldPosition + (cameraNode.Rotation * new Vector3(0.0f, 0.0f, 1.0f));
		const float objectVelocity = 10.0f;
		body.SetLinearVelocity(cameraNode.WorldRotation * new Vector3(0.0f, 0.0f, 1.0f) * objectVelocity);
    }

    void OnCollision(PhysicsCollisionEvent eventData)
    {
        
    }
}

public class ProjectileBullet : Projectile
{
    public void Start(){
		this.modelName = "Models/bullet.fbx";
		this.impactType = "Point";
    }
}