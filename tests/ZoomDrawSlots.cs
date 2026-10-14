using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ActEditor.Core;
using ActEditor.Core.DrawingComponents;
using ActEditor.Core.WPF.EditorControls;
using ActEditor.Core.WPF.FrameEditor;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using GRF.Image;
using Frame = GRF.FileFormats.ActFormat.Frame;

public sealed class ZoomDrawSlots : IFrameRendererEditor {
    public Act Act { get; private set; }
    public FrameRenderer FrameRenderer { get; private set; }
    public int SelectedAction { get { return 0; } }
    public int SelectedFrame { get { return 0; } }
    public UIElement Element { get { return FrameRenderer; } }
    public SelectionEngine SelectionEngine { get { return null; } }
    public List<ReferenceControl> References { get { return new List<ReferenceControl>(); } }
    public IActIndexSelector IndexSelector { get { return null; } }
    public Grid GridPrimary { get { return null; } }
    public LayerEditor LayerEditor { get { return null; } }
    public SpriteSelector SpriteSelector { get { return null; } }
    public SpriteManager SpriteManager { get { return null; } }
    public event ActEditorWindow.ActEditorEventDelegate ReferencesChanged { add {} remove {} }
    public event ActEditorWindow.ActEditorEventDelegate ActLoaded { add {} remove {} }

    private static Act Create(string name, int layerCount) {
        var sprite = new Spr();
        var image = new GrfImage(new byte[] { 0, 0, 255, 255, 0, 255, 0, 255 }, 2, 1, GrfImageType.Bgra32);
        sprite.InsertAny(image);
        var act = new Act(sprite) { Name = name };
        var action = new GRF.FileFormats.ActFormat.Action();
        var frame = new Frame();
        for (int i = 0; i < layerCount; i++)
            frame.Layers.Add(new Layer(0, image) { Mirror = name != null, OffsetX = 20 * i, OffsetY = -81 });
        action.Frames.Add(frame);
        act.Actions.Add(action);
        return act;
    }

    private static void Render(FrameRenderer renderer) {
        renderer.DrawSlotManager.Begin();
        renderer.DrawIndex = 0;
        foreach (var component in renderer.Components) component.Render(renderer);
        renderer.DrawIndex = -1;
        renderer.DrawSlotManager.End();
    }

    public static void Run() {
        var fixture = new ZoomDrawSlots();
        fixture.Act = Create(null, 1);
        fixture.FrameRenderer = new FrameRenderer { Editor = fixture };
        var renderer = fixture.FrameRenderer;
        renderer.Measure(new Size(400, 400));
        renderer.Arrange(new Rect(0, 0, 400, 400));
        var back = Create("Other", 0);
        var front = Create("Nearby", 2);
        renderer.Components.Add(new ActDraw(back, fixture));
        renderer.Components.Add(new ActDraw(fixture.Act, fixture));
        renderer.Components.Add(new ActDraw(front, fixture));
        Render(renderer);

        // History/frame changes can alter the number of reference layers.
        // Slots formerly used by the front reference now belong to the accessory.
        back[0, 0].Layers.Add(new Layer(front[0, 0, 0]));
        back[0, 0].Layers.Add(new Layer(front[0, 0, 1]));
        front[0, 0].Layers.Clear();
        Render(renderer);
        var accessory = renderer.DrawSlotManager.DrawSlots[2].Image;
        foreach (double zoom in new[] { 0.5, 2.0, 1.0 }) {
            renderer.ZoomEngine.SetZoom(zoom);
            renderer.SizeUpdate();
            var actual = accessory.RenderTransform.Value;
            var expected = new Matrix(1, 0, 0, 1, -1, -82) * renderer.View;
            if (actual != expected)
                throw new Exception("Zoom reused a reference transform for the accessory: expected " + expected + ", got " + actual);
            if (fixture.Act[0, 0, 0].Mirror || fixture.Act[0, 0, 0].OffsetY != -81)
                throw new Exception("Zoom modified the accessory data.");
            Console.WriteLine("PASS accessory orientation and placement at zoom " + zoom);
        }
        renderer.Dispose();
    }
}
