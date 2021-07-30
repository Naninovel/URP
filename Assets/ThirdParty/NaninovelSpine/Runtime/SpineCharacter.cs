using System;
using System.Threading.Tasks;
using Naninovel.Commands;
using Naninovel.FX;
using UniRx.Async;
using UnityEngine;

namespace Naninovel
{
    /// <summary>
    /// A <see cref="ICharacterActor"/> implementation using <see cref="SpineController"/> to represent an actor.
    /// </summary>
    /// <remarks>
    /// Spine character prefab is expected to have a <see cref="SpineController"/> component attached to the root object.
    /// </remarks>
    [ActorResources(typeof(SpineController), false)]
    public class SpineCharacter : MonoBehaviourActor<CharacterMetadata>, ICharacterActor, LipSync.IReceiver, Blur.IBlurable
    {
        /// <summary>
        /// Controller component of the instantiated spine prefab associated with the actor.
        /// </summary>
        public virtual SpineController Controller { get; private set; }
        public override string Appearance { get => appearance; set => SetAppearance(value); }
        public override bool Visible { get => visible; set => SetVisibility(value); }
        public CharacterLookDirection LookDirection
        {
            get => TransitionalRenderer.GetLookDirection(ActorMetadata.BakedLookDirection);
            set => TransitionalRenderer.SetLookDirection(value, ActorMetadata.BakedLookDirection);
        }

        protected virtual TransitionalRenderer TransitionalRenderer { get; private set; }
        protected virtual SpineDrawer Drawer { get; private set; }
        protected virtual CharacterLipSyncer LipSyncer { get; private set; }

        private LocalizableResourceLoader<GameObject> prefabLoader;
        private string appearance;
        private bool visible;

        public SpineCharacter (string id, CharacterMetadata metadata)
            : base(id, metadata) { }

        public override async UniTask InitializeAsync ()
        {
            await base.InitializeAsync();

            prefabLoader = InitializeLoader(ActorMetadata);
            Controller = await InitializeControllerAsync(prefabLoader, Id, Transform);
            TransitionalRenderer = TransitionalRenderer.CreateFor(ActorMetadata, GameObject, true);
            Drawer = new SpineDrawer(Controller);
            LipSyncer = new CharacterLipSyncer(Id, Controller.ChangeIsSpeaking);

            SetVisibility(false);

            Engine.Behaviour.OnBehaviourUpdate += DrawSpine;
        }

        public override void Dispose ()
        {
            if (Engine.Behaviour != null)
                Engine.Behaviour.OnBehaviourUpdate -= DrawSpine;

            LipSyncer.Dispose();
            Drawer.Dispose();

            base.Dispose();

            prefabLoader?.UnloadAll();
        }

        public UniTask BlurAsync (float duration, float intensity, EasingType easingType = default, CancellationToken cancellationToken = default)
        {
            return TransitionalRenderer.BlurAsync(duration, intensity, easingType, cancellationToken);
        }

        public override UniTask ChangeAppearanceAsync (string appearance, float duration, EasingType easingType = default,
            Transition? transition = default, CancellationToken cancellationToken = default)
        {
            this.appearance = appearance;
            if (Controller)
                Controller.ChangeAppearance(appearance, duration, easingType, transition);
            return UniTask.CompletedTask;
        }

        public UniTask ChangeLookDirectionAsync (CharacterLookDirection lookDirection, float duration,
            EasingType easingType = default, CancellationToken cancellationToken = default)
        {
            return TransitionalRenderer.ChangeLookDirectionAsync(lookDirection,
                ActorMetadata.BakedLookDirection, duration, easingType, cancellationToken);
        }

        public override async UniTask ChangeVisibilityAsync (bool visible, float duration, EasingType easingType = default,
            CancellationToken cancellationToken = default)
        {
            this.visible = visible;
            await TransitionalRenderer.FadeToAsync(visible ? TintColor.a : 0, duration, easingType, cancellationToken);
        }

        public void AllowLipSync (bool active) => LipSyncer.SyncAllowed = active;

        protected virtual void SetAppearance (string appearance)
        {
            this.appearance = appearance;
            if (Controller)
                Controller.ChangeAppearance(appearance);
        }

        protected virtual void SetVisibility (bool visible) => ChangeVisibilityAsync(visible, 0).Forget();

        protected override Color GetBehaviourTintColor () => TransitionalRenderer.TintColor;

        protected override void SetBehaviourTintColor (Color tintColor)
        {
            if (!Visible) // Handle visibility-controlled alpha of the tint color.
                tintColor.a = TransitionalRenderer.TintColor.a;
            TransitionalRenderer.TintColor = tintColor;
        }

        protected virtual void DrawSpine () => Drawer.DrawTo(TransitionalRenderer, ActorMetadata.PixelsPerUnit);

        private static LocalizableResourceLoader<GameObject> InitializeLoader (ActorMetadata actorMetadata)
        {
            var providerManager = Engine.GetService<IResourceProviderManager>();
            var localizationManager = Engine.GetService<ILocalizationManager>();
            return actorMetadata.Loader.CreateLocalizableFor<GameObject>(providerManager, localizationManager);
        }

        private static async Task<SpineController> InitializeControllerAsync (LocalizableResourceLoader<GameObject> loader, string actorId, Transform transform)
        {
            var prefabResource = await loader.LoadAsync(actorId);
            if (!prefabResource.Valid)
                throw new Exception($"Failed to load Spine prefab for `{actorId}` character. Make sure the resource is set up correctly in the character configuration.");
            var controller = Engine.Instantiate(prefabResource.Object).GetComponent<SpineController>();
            controller.gameObject.name = actorId;
            controller.transform.SetParent(transform);
            return controller;
        }
    }
}
