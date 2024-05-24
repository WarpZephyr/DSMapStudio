using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace StudioCore.Resource;
public static partial class ResourceManager
{
    /// <summary>
    ///     A named job that runs many tasks and whose progress will appear in the progress window.
    /// </summary>
    public class ResourceJob
    {
        /// <summary>
        /// An block that loads every binder resource passed to it.
        /// </summary>
        private readonly ActionBlock<LoadBinderResourcesAction> _loadBinderResources;

        /// <summary>
        /// A block that loads every texture resource passed to it.
        /// </summary>
        private readonly TransformManyBlock<LoadTPFResourcesAction, LoadTPFTextureResourceRequest> _loadTPFResources;

        /// <summary>
        /// The processed resource replies waiting to be used.
        /// </summary>
        private readonly BufferBlock<ResourceLoadedReply> _processedResources;
        private int _courseSize;
        private int TotalSize;

        // Asset load pipelines
        internal IResourceLoadPipeline FlverLoadPipeline { get; }
        internal IResourceLoadPipeline HavokCollisionLoadPipeline { get; }
        internal IResourceLoadPipeline HavokNavmeshLoadPipeline { get; }
        internal IResourceLoadPipeline NVMNavmeshLoadPipeline { get; }
        internal IResourceLoadPipeline TPFTextureLoadPipeline { get; }

        /// <summary>
        /// The name of the <see cref="ResourceJob"/>.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The completion progress of the <see cref="ResourceJob"/>.
        /// </summary>
        public int Progress { get; private set; }

        /// <summary>
        /// Whether or not the <see cref="ResourceJob"/> has finished it's tasks.
        /// </summary>
        public bool Finished { get; private set; }

        /// <summary>
        /// Create a new <see cref="ResourceJob"/> with the given name.
        /// </summary>
        /// <param name="name">The name of the <see cref="ResourceJob"/>.</param>
        public ResourceJob(string name)
        {
            ExecutionDataflowBlockOptions options = new() { MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded };
            Name = name;
            _loadTPFResources =
                new TransformManyBlock<LoadTPFResourcesAction, LoadTPFTextureResourceRequest>(LoadTPFResources,
                    options);

            //options.MaxDegreeOfParallelism = 4;
            _loadBinderResources = new ActionBlock<LoadBinderResourcesAction>(LoadBinderResources, options);
            _processedResources = new BufferBlock<ResourceLoadedReply>();

            FlverLoadPipeline = new ResourceLoadPipeline<FlverResource>(_processedResources);
            HavokCollisionLoadPipeline = new ResourceLoadPipeline<HavokCollisionResource>(_processedResources);
            HavokNavmeshLoadPipeline = new ResourceLoadPipeline<HavokNavmeshResource>(_processedResources);
            NVMNavmeshLoadPipeline = new ResourceLoadPipeline<NVMNavmeshResource>(_processedResources);
            TPFTextureLoadPipeline = new TextureLoadPipeline(_processedResources);
            _loadTPFResources.LinkTo(TPFTextureLoadPipeline.LoadTPFTextureResourceRequest);
        }

        internal void IncrementEstimateTaskSize(int size)
        {
            Interlocked.Add(ref TotalSize, size);
        }

        internal void IncrementCourseEstimateTaskSize(int size)
        {
            Interlocked.Add(ref _courseSize, size);
        }

        /// <summary>
        ///     Get an estimate of the size of a task (i.e. how many files to load)
        /// </summary>
        /// <returns></returns>
        public int GetEstimateTaskSize()
        {
            return Math.Max(TotalSize, _courseSize);
        }

        internal void AddLoadTPFResources(LoadTPFResourcesAction action)
        {
            _loadTPFResources.Post(action);
        }

        internal void AddLoadBinderResources(LoadBinderResourcesAction action)
        {
            _courseSize++;
            _loadBinderResources.Post(action);
        }

        /// <summary>
        /// Return a <see cref="Task"/> that represents the completion of this <see cref="ResourceJob"/>'s tasks.
        /// </summary>
        /// <returns>A <see cref="Task"/> that represents the completion of this <see cref="ResourceJob"/>'s tasks.</returns>
        public Task Complete()
        {
            // Transform all the tasks into one task
            return JobTaskFactory.StartNew(() =>
            {
                _loadBinderResources.Complete();
                _loadBinderResources.Completion.Wait();
                FlverLoadPipeline.LoadByteResourceBlock.Complete();
                FlverLoadPipeline.LoadFileResourceRequest.Complete();
                HavokCollisionLoadPipeline.LoadByteResourceBlock.Complete();
                HavokCollisionLoadPipeline.LoadFileResourceRequest.Complete();
                HavokNavmeshLoadPipeline.LoadByteResourceBlock.Complete();
                HavokNavmeshLoadPipeline.LoadFileResourceRequest.Complete();
                _loadTPFResources.Complete();
                _loadTPFResources.Completion.Wait();
                TPFTextureLoadPipeline.LoadTPFTextureResourceRequest.Complete();
                FlverLoadPipeline.LoadByteResourceBlock.Completion.Wait();
                FlverLoadPipeline.LoadFileResourceRequest.Completion.Wait();
                HavokCollisionLoadPipeline.LoadByteResourceBlock.Completion.Wait();
                HavokCollisionLoadPipeline.LoadFileResourceRequest.Completion.Wait();
                HavokNavmeshLoadPipeline.LoadByteResourceBlock.Completion.Wait();
                HavokNavmeshLoadPipeline.LoadFileResourceRequest.Completion.Wait();
                TPFTextureLoadPipeline.LoadTPFTextureResourceRequest.Completion.Wait();
                Finished = true;
            });
        }

        /// <summary>
        /// Process each <see cref="ResourceLoadedReply"/> in this <see cref="ResourceJob"/> and notify the listeners.
        /// </summary>
        public void ProcessLoadedResources()
        {
            // Process any resource load replies for the database and notify the listeners.
            if (_processedResources.TryReceiveAll(out IList<ResourceLoadedReply> processed))
            {
                Progress += processed.Count;
                foreach (ResourceLoadedReply p in processed)
                {
                    // Flatten the name of the resource to lowercase for the database
                    var lPath = p.VirtualPath.ToLower();

                    // Try to get the resource from the database and add it if it doesn't already exist.
                    if (!ResourceDatabase.TryGetValue(lPath, out IResourceHandle reg))
                    {
                        reg = ConstructHandle(p.Resource.GetType(), p.VirtualPath);
                        ResourceDatabase.Add(lPath, reg);
                    }

                    // Notify that the resource has been loaded.
                    reg._ResourceLoaded(p.Resource, p.AccessLevel);
                }
            }
        }
    }
}
