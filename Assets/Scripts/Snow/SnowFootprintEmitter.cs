using UnityEngine;

namespace SnowTracks
{
    // Distance accumulator is independent of Update FPS and the player's aim direction.
    public sealed class SnowStepSampler
    {
        Vector2 previous; float remaining; bool hasPrevious; bool left;
        public void Reset() { hasPrevious=false;remaining=0;left=false; }
        public void Sample(Vector2 point,float spacing,float separation,System.Action<Vector2,Vector2> stamp)
        {
            spacing=Mathf.Max(.05f,spacing);
            if(!hasPrevious){previous=point;hasPrevious=true;remaining=spacing;return;}
            Vector2 delta=point-previous;float distance=delta.magnitude;
            if(distance>2){Reset();previous=point;hasPrevious=true;remaining=spacing;return;}
            if(distance<.00001f)return;
            Vector2 direction=delta/distance;float travelled=0;
            while(distance-travelled+1e-6f>=remaining)
            {
                travelled+=remaining;
                Vector2 sideways=new Vector2(direction.y,-direction.x)*(left?-separation:separation);
                stamp(previous+direction*travelled+sideways,direction);left=!left;remaining=spacing;
            }
            remaining-=distance-travelled;previous=point;
        }
    }
    [DisallowMultipleComponent,DefaultExecutionOrder(1200)]
    public sealed class SnowFootprintEmitter : MonoBehaviour
    {
        public SnowTrackWorld world;
        public Transform footAnchor;
        [Min(.05f)] public float stepSpacing=.45f;
        [Min(0)] public float footSeparation=.10f;
        public Vector2 footprintSize=new Vector2(.22f,.40f);
        [Min(.01f)] public float trailWidth=.55f;
        [Tooltip("FullSnow only. 0: footprints only; 1: normal compaction; 10: a strongly packed path.")]
        [Range(0,10)] public float trailStrength=1;
        [Tooltip("Fill the path between footsteps along the centre of movement. FullSnow only.")]
        public bool continuousTrail;
        readonly SnowStepSampler trailSteps=new SnowStepSampler();
        readonly SnowStepSampler steps=new SnowStepSampler();
        System.Action<Vector2,Vector2> trailCallback;
        Rigidbody2D body; SurvivalCombat.Health health;SurvivalCombat.CombatActor actor;FlyingMode flight;
        Vector2 localAnchorOffset;
        System.Action<Vector2,Vector2> stampCallback;
        void Start()
        {
            stampCallback=Stamp;trailCallback=StampTrail;
            body=GetComponent<Rigidbody2D>();health=GetComponent<SurvivalCombat.Health>();actor=GetComponent<SurvivalCombat.CombatActor>();
            flight=FindAnyObjectByType<FlyingMode>();
            if(footAnchor==null)
            {
                var anchor=new GameObject("FootAnchor");anchor.transform.SetParent(transform,false);footAnchor=anchor.transform;
                foreach(var sprite in GetComponentsInChildren<SpriteRenderer>())
                {
                    if(sprite.sprite==null||sprite.name.Contains("Weapon"))continue;
                    footAnchor.position=new Vector3(transform.position.x,sprite.bounds.min.y,transform.position.z);break;
                }
            }
            ResetPath();
        }
        public void ResetPath() { steps.Reset();trailSteps.Reset();if(footAnchor!=null)localAnchorOffset=footAnchor.position-transform.position; }
        public static void ResetFor(GameObject player) { var emitter=player.GetComponent<SnowFootprintEmitter>();if(emitter!=null)emitter.ResetPath(); }
        void OnEnable(){ResetPath();}
        void OnDisable(){ResetPath();}
        void Update()
        {
            if(Time.timeScale<=0)ResetPath();
        }
        void Awake()
        {
            // Выведет в консоль всю цепочку вызовов, которая создала этот скрипт
            Debug.Log("Меня создали отсюда:\n" + System.Environment.StackTrace);
        }
        void FixedUpdate()
        {
            if(world==null||!world.TracksAvailable||Time.timeScale<=0||(health!=null&&health.Dead)||(flight!=null&&flight.isFlying)||
                (actor!=null&&(actor.InventoryOpen||actor.InFlight))) { ResetPath();return; }
            // Rigidbody.position avoids interpolation-dependent spacing. The unscaled visual foot offset stays in Unity coordinates.
            Vector2 position=(body!=null?body.position:(Vector2)transform.position)+localAnchorOffset;
            var mapPosition=WorldProjection.ToMap(position);
            steps.Sample(mapPosition,stepSpacing,footSeparation,stampCallback);
            if(continuousTrail && trailStrength>0 && world.Mode==SnowTrackMode.FullSnow)
                trailSteps.Sample(mapPosition,Mathf.Clamp(trailWidth*.25f,.05f,.1f),0,trailCallback);
            else trailSteps.Reset();
        }
        void StampTrail(Vector2 point,Vector2 direction) { world.Stamp(point,direction,Vector2.zero,trailWidth,trailStrength,true); }
        void Stamp(Vector2 point,Vector2 direction) { world.Stamp(point,direction,footprintSize,trailWidth,continuousTrail?0:trailStrength); }
    }
}
