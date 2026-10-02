import unittest
from score_transcript import score, words

class ScoringChecks(unittest.TestCase):
    def test_exact(self):
        self.assertEqual(score(words('Genesis 49:3'),words('GENESIS 49:3.'))['wer'],0)
    def test_substitution(self):
        self.assertEqual(score(['my','might'],['my','night'])['substitutions'],1)
    def test_deleted_reference(self):
        result=score(['genesis','one','one'],[])
        self.assertEqual(result['deletions'],3)
    def test_hallucinations_and_overlap(self):
        result=score(['hello'],['hello','genesis','genesis'])
        self.assertEqual(result['insertions'],2)
        self.assertEqual(result['wer'],2)
    def test_no_fabricated_ground_truth(self):
        with self.assertRaises(ValueError):score([],['genesis'])
    def test_numbers_not_silently_corrected(self):
        self.assertEqual(score(words('verse three'),words('verse CS3'))['substitutions'],1)

if __name__=='__main__':unittest.main()
