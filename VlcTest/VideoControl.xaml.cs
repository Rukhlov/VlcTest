using System.Windows.Controls;

namespace VlcTest
{
    public partial class VideoControl : UserControl
    {
        public VideoControl()
        {
            InitializeComponent();

            // this.DataContext = this;

            // this.Video.SetBinding(Image.SourceProperty, new Binding(nameof(VideoSource)));

            //this.IsVisibleChanged += (o, a) => 
            //{

            //    if((bool)a.NewValue)
            //    {
            //        ((VideoSourceProvider)this.DataContext).Renderer.Run(this.Video);
            //    }
            //};
        }

        
    }

}
