using Application.Graphics;
using Application.Models;

namespace Application.ECS.Component
{
    internal struct Renderable
    {
        public required VIBufferView[] VIBufferViews { get; init; }
        public required Surface[] Surfaces { get; init; }
    }
}
