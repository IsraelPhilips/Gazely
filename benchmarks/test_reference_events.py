import unittest
from score_reference_events import evaluate

def row(event='final',ref='Leviticus 6:13',wall=3500):
    return dict(event=event,reference=ref,wall_ms=str(wall),audio_start_ms='0',audio_end_ms='2500')

class ReferenceScores(unittest.TestCase):
    def annotation(self,verified=True):
        return dict(events=[dict(start_ms=500,end_ms=2000,reference='Leviticus 6:13',kind='explicit',verified=verified)])
    def test_unknown_truth_excluded(self):
        self.assertIsNone(evaluate([row()],self.annotation(False),True)['exact_reference_recall'])
    def test_incorrect_chapter_not_a_hit(self):
        self.assertEqual(evaluate([row(ref='Leviticus 11:13')],self.annotation(),True)['exact_reference_recall'],0)
    def test_provisional_not_committed(self):
        result=evaluate([row('partial')],self.annotation(),True)
        self.assertEqual(result['exact_reference_recall'],0)
        self.assertEqual(result['events'][0]['first_visible_ms'],1500)
    def test_accelerated_has_no_latency_claim(self):
        result=evaluate([row()],self.annotation(),False)
        self.assertEqual(result['exact_reference_recall'],1)
        self.assertIsNone(result['events'][0]['first_committed_ms'])
    def test_noise_false_call(self):
        result=evaluate([row()],dict(events=[],negative_spans=[dict(start_ms=0,end_ms=4000,verified=True)]),True)
        self.assertEqual(len(result['false_reference_events']),1)

if __name__=='__main__':unittest.main()
