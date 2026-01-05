using ChepInlineApp.Classifier.Models;

namespace ChepInlineApp.Classifier.Core
{
    public class ClassifierPostProcessor
    {
        private readonly string[] _classLabels;

        public ClassifierPostProcessor(string[] classLabels)
        {
            _classLabels = classLabels;
        }

        public ClassificationObject Process(float[] logits)
        {
            // Apply softmax
            float[] probs = Softmax(logits);

            // Argmax
            int classId = Array.IndexOf(probs, probs.Max());
            float confidence = probs[classId];

            return new ClassificationObject
            {
                ClassID = classId,
                Label = _classLabels[classId],
                Probability = confidence
            };
        }

        private float[] Softmax(float[] logits)
        {
            // Numeric stability trick: subtract max logit
            float maxLogit = logits.Max();
            double[] exps = logits.Select(x => Math.Exp(x - maxLogit)).ToArray(); // numberical stability
            double sumExp = exps.Sum();
            return exps.Select(e => (float)(e / sumExp)).ToArray();
        }
    }

}
