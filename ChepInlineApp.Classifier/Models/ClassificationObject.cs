using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChepInlineApp.Classifier.Models
{
    public class ClassificationObject
    {
        public int ClassID { get; set; }
        public string Label { get; set; }
        public float Probability { get; set; }
    }
}
