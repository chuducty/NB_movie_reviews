﻿// Implementation of the Multinomial Naive Bayes classifier to classify movie reviews 
// from the popular polarityv2 dataset.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

    namespace ImpNaiveBay1
{
    /// <summary>
    /// This program implements the multinomial Naive Bayes algorithm as described in:
    /// [REF:Speech and Language Processing. Daniel Jurafsky and James H. Martin. Copyright 2016.]
    /// The program starts with loading all the input files required:
    /// 1- the document corpus;
    /// 2- a list of stop words;
    /// 3- 10 fold cross-validation indices.
    /// It then loops through each cross-validation fold carrying out the following steps:
    /// 4- selects the docs for train and test datasets (from document corpus) based on 
    ///     cross-validation indicies for current iteration;
    /// 5- creates a vocabulary based on train dataset;
    /// 6- calculates loglikelihoods and logpriors for each class using the train dataset;
    /// 7- calculates performance on test dataset. 
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("*************************************************************");
            Console.WriteLine("**********Implementation of Multinomial Naive Bayes**********");
            Console.WriteLine("*********Application to movie review classification**********");
            Console.WriteLine("*************************************************************");

            // load the whole review corpus.
            string root_dir = Directory.GetCurrentDirectory() + "\\txt_sentoken\\";
            // store reviews in a list of strings.
            List<string> doc_corpus = new List<string>();
            doc_corpus = LoadCorpus(root_dir);

            // load stop-words - use the scikit-learn list of stopwords consisting of 318 
            // common words.
            string file_stopw = root_dir + "scikit_stopw.txt";
            Dictionary<string, int> dic_stopw = new Dictionary<string, int>();
            LoadDictionary(dic_stopw, file_stopw);

            // load train and test indices. These were generated with sciki-learn
            // StratifiedKFold function using 10 folds. Train/test split is 90/10. 
            // Note: We would need to implement our version of StratifiedKFold if we 
            // wanted the train/test indices to vary at each model run.
            int nsplits = 10;
            var (l1, l2) = LoadIndices(root_dir, nsplits);

            // Vector of labels indicating whether the review is positive or
            // negative. 1: inditcates positive and 0: indicates negative
            // Keep same order as document corpus: first 1000 reviews are positive,
            // remaining 1000 reviews are negative.
            int[] labels_ = Enumerable.Repeat(0, 1000).ToArray();
            int[] labels = Enumerable.Repeat(1, 2000).ToArray();
            Array.Copy(labels_, 0, labels, 1000, 1000);

            // main cross-validation loop 
            for(int ifold = 0; ifold < nsplits; ifold++)
            {
                // get train and test set
                var (xtrain, xtest, ytrain, ytest) = GetSets(doc_corpus, labels,
                                                              l1[ifold], l2[ifold]);
                // create vocabulary
                // N.B. only use docs from train dataset
                Dictionary<string, int> vocab = new Dictionary<string, int>();
                CreateVocab(xtrain, dic_stopw, vocab);

                // calculate loglikelihoods, logprior for each class (positive and negative)
                var (loglp, logpp) = NBproba(vocab, dic_stopw, xtrain, ytrain, "positive");
                var (logln, logpn) = NBproba(vocab, dic_stopw, xtrain, ytrain, "negative");

                // let's assess the performance of the multinomial Naive Bayes on the test dataset
                double accuracy = TestNaiveBayes(vocab, dic_stopw, xtest, ytest, loglp,
                                                   logpp, logln, logpn);

                Console.WriteLine("Accuracy for fold {0} is: {1}%", ifold, accuracy);
                
            }
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }

        /// <summary>
        /// Function LoadCorpus(). Loads the document corpus into a list. Positively
        /// labelled documents are processed first (first 1000 documents) followed 
        /// by negatively labelled documents (next 1000 documents)
        /// </summary>
        /// <param name="str">path to loaction of positive and negative reviews. The path
        /// is assumed to be current_directory\txt_sentoken\ where current_directory is:
        /// .\ImpNaiveBay1\ImpNaiveBay1\bin\Debug\</param>
        /// <returns>A list of strings. Each string is a text document in the corpus</returns>
        static List<string> LoadCorpus(string str)
        {
            // List with documents corpus
            List<string> corpus = new List<string>();

            // load positive reviews first
            // list_pos.txt provides a list of all positive reviews
            string file_name_pos = "list_pos.txt";
            // positive reviews are stored into txt_sentoken\pos\
            string pos_root = "\\pos\\";
            string file_list_pos = str + pos_root + file_name_pos;

            string line;
            System.IO.StreamReader file = new System.IO.StreamReader(file_list_pos);
            while ((line = file.ReadLine()) != null)
            {
                string[] words = line.Split(' ').Where(s => s != String.Empty).ToArray<string>();

                // fourth token contains name of file
                string file_to_proc = str + pos_root + words[4];

                // add to corpus
                corpus.Add(LoadFile(file_to_proc));
            }
            file.Close();

            // load negative reviews
            // list_neg.txt provides a list of all negative reviews
            string file_name_neg = "list_neg.txt";
            // negative reviews are stored into txt_sentoken\neg\
            string neg_root = "\\neg\\";
            string file_list_neg = str + neg_root + file_name_neg;

            file = new System.IO.StreamReader(file_list_neg);
            while ((line = file.ReadLine()) != null)
            {
                string[] words = line.Split(' ').Where(s => s != String.Empty).ToArray<string>();

                // fourth token contains name of file
                string file_to_proc = str + neg_root + words[4];

                // add to corpus
                corpus.Add(LoadFile(file_to_proc));
            }
            file.Close();

            // return documents corpus
            return corpus;
        }

        /// <summary>
        /// Function LoadFile(). Converts the content of a file into a string.
        /// </summary>
        /// <param name="str">full path to file to be processed</param>
        /// <returns>A string representing the text document</returns>
        static string LoadFile(string str)
        {
            // build string for review being processed
            string doc_string = "";
            string line;
            System.IO.StreamReader file = new System.IO.StreamReader(str);
            while ((line = file.ReadLine()) != null)
            {
                doc_string += line;
            }

            file.Close();

            // return text document
            return doc_string;
        }

        /// <summary>
        /// Function LoadIndices(). Stores document indices for the train and test 
        /// datasets into two separate lists. Each list contains ten sublists (one for each
        /// cross-validation fold). The indices were generated using the StratifiedKFold function
        /// in scikit-learn
        /// </summary>
        /// <param name="str">path to file containing the train and test indices</param>
        /// <param name="splits">the number of cross-validation splits (generally 10)</param>
        /// <returns>A tuple of nested lists of indices for the train (list1) and test (list2)
        /// datasets.</returns>
        public static (List<List<int>> list1, List<List<int>> list2) 
            LoadIndices(string str, int splits)
        {
            // create nsplits sublists and add them to list
            // a list for each pair train and test sets
            // initialize lists
            List<List<int>> list1 = new List<List<int>>();
            List<List<int>> list2 = new List<List<int>>();
            // initialize sublists
            for (int i=0; i<splits; i++)
            {
                List<int> sublist1 = new List<int>();
                list1.Add(sublist1);
                List<int> sublist2 = new List<int>();
                list2.Add(sublist2);
            }

            // file containing indices for train dataset
            // the file is comma separated. Each column is a 
            // sequence of indices (each index correpsond to a movie
            // review)
            string file_train_ind = str + "train_indexes.txt";

            string line;
            System.IO.StreamReader file = new System.IO.StreamReader(file_train_ind);
            // skip header - from pandas dataframe
            line = file.ReadLine();
            while ((line = file.ReadLine()) != null)
            {
                string[] indices = line.Split(',');
                for (int i=1; i<splits+1; i++)
                {
                    int j = Int32.Parse(indices[i]);
                    // add index to each sublist 
                    list1[i - 1].Add(j);
                }
            }
            file.Close();

            // do the same for the test dataset
            // file containing indices for test dataset
            string file_test_ind = str + "test_indexes.txt";

            file = new System.IO.StreamReader(file_test_ind);
            // skip header
            line = file.ReadLine();
            while ((line = file.ReadLine()) != null)
            {
                string[] indices = line.Split(',');
                for (int i = 1; i < splits + 1; i++)
                {
                    int j = Int32.Parse(indices[i]);
                    // add index to each sublist 
                    list2[i - 1].Add(j);
                }
            }
            file.Close();

            // return the two lists
            return (list1, list2);
        }


        /// <summary>
        /// Function GetSets(). Selects docs from corpus for train and test datasets
        /// according to cross-validation indices for the current cross-validation iteration.
        /// </summary>
        /// <param name="docs">The document corpus</param>
        /// <param name="labels">The documents labels (either positive or negative)</param>
        /// <param name="list1">List of indices used to select documents for the train dataset
        /// from the corpus for the current cross-validation iteration</param>
        /// <param name="list2">List of indices used to select documents for the test dataset
        /// from the corpus for the current cross-validation iteration</param>
        /// <returns>
        /// Tuple (xtrain, xtest, ytrain, ytest), xtrain and xtest are the selected
        /// documents for the train and test datasets, respectively, for the current cross-validation
        /// iteration. ytrain and ytest are the corresponding labels.
        /// </returns>
        public static (List<string> xtrain, List<string> xtest,
            List<int> ytrain, List<int> ytest) GetSets(List<string> docs, int[] labels,
                                                              List<int> list1, List<int> list2)
        {
            List<string> xtrain = new List<string>();
            List<string> xtest = new List<string>();
            List<int> ytrain = new List<int>();
            List<int> ytest = new List<int>();

            xtrain = SelectDocs(list1, docs);
            xtest = SelectDocs(list2, docs);
            ytrain = SelectLabels(list1, labels);
            ytest = SelectLabels(list2, labels);

            return (xtrain, xtest, ytrain, ytest);

        }

        /// <summary>
        /// Function SelectDocs(). Selects documents from a list at specific
        /// index locations.
        /// </summary>
        /// <param name="l">The list of indices to use for selection</param>
        /// <param name="d">The list of text docs to select from</param>
        /// <returns></returns>
        public static List<string> SelectDocs(List<int>l, List<string> d)
        {
            List<string> lst = new List<string>();
            for (int i = 0; i < l.Count; i++)
            {
                lst.Add(d[l[i]]);
            }

            return lst;
        }

        /// <summary>
        /// Function SelectLabels(). Selects integers (labels) from a list at
        /// specific index locations.
        /// </summary>
        /// <param name="l">The list of indices to use for selection</param>
        /// <param name="d">The integer array to select from</param>
        /// <returns></returns>
        public static List<int> SelectLabels(List<int> l, int[] d)
        {
            List<int> lst = new List<int>();
            for (int i = 0; i < l.Count; i++)
            {
                lst.Add(d[l[i]]);
            }

            return lst;
        }

        /// <summary>
        /// Function LoadDictionary(). Adds text tokens to a dictionary from a provided 
        /// input file containing a list of strings.
        /// </summary>
        /// <param name="adictionary">dictionary to load</param>
        /// <param name="file_">full path to file with data to load into the dictionary</param>
        static void LoadDictionary(Dictionary<string, int> adictionary,
                                      String file_)
        {
            string stopw;
            System.IO.StreamReader infile = new System.IO.StreamReader(file_);
            while ((stopw = infile.ReadLine()) != null)
            {
                // if not already in dictionary add it
                // there should not be any duplicates anyway.
                if (!adictionary.ContainsKey(stopw.Trim()))
                {
                    adictionary.Add(stopw.Trim(), 1);
                }
            }

        }

        /// <summary>
        /// Function CreateVocab(). Splits each string document in the train dataset into tokens.
        /// For each token created checks whether it is a stop word or not. If it is
        /// a stop word the token is discarded otherwise the token is added to the vocabulary.
        /// </summary>
        /// <param name="docs">The document train dataset used to create the vocabulary</param>
        /// <param name="stopw">The set of common words to remove from the vocabulary</param>
        /// <param name="V">The vocabulary created for the current cross-validation iteration</param>
        static void CreateVocab(List<string> docs,
                                 Dictionary<string, int> stopw,
                                 Dictionary<string, int> V)
        {
            for(int i=0; i<docs.Count; i++)
            {
                // tockenize the review

                string[] tokens = SplitWords(docs[i]);

                for (var j = 0; j < tokens.Length; j++)
                {
                    // update the term frequency dictionary
                    if (!stopw.ContainsKey(tokens[j]))
                    {
                        // add unique tokens to temporary dictionary
                        if (!V.ContainsKey(tokens[j].Trim()))
                        {
                            V.Add(tokens[j].Trim(), 1);
                        }
                        else
                        {
                            V[tokens[j].Trim()] += 1;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Function SplitWords(). Splits a string into tokens based on Regex matching.
        /// Currently the function splits based on non-alphanumerics characters including '_',
        /// however for words with possession or negation such as wouldn't or Luca's the function
        /// does not splits the apostrophes. Therefore, words like wouldn't or Luca's are not 
        /// split into two separate tokens 
        /// </summary>
        /// <param name="str">The string to be tockenized.</param>
        /// <returns>An array of text tokens</returns>
        static string[] SplitWords(string str)
        {
            //
            // Split on all non-word characters.
            // ... Returns an array of all the words.
            //
            // Various Alternatives for Regex:
            //return Regex.Split(s, @"\W+");
            //return Regex.Split(str.ToLower(), @"\W+").Where(s => s != String.Empty).ToArray<string>();
            //return Regex.Split(str.ToLower(), @"[^a-zA-Z0-9_']").Where(s => s != String.Empty).ToArray<string>();
            //return Regex.Split(str.ToLower(), @"(?i)(?<=^|\s)([a-z]+('[a-z]*)?|'[a-z]+)(?=\s|$)").Where(s => s != String.Empty).ToArray<string>();
            //return Regex.Matches(str.ToLower(), "\\w+('(s|d|t|ve|mon|ll|m|re))?").Cast<Match>().Select(x => x.Value).ToArray();
            // revised to this to allow split with _ as well
            //
            return Regex.Matches(str.ToLower(), "[a-zA-Z0-9]+('(s|d|t|ve|mon|ll|m|re))?").Cast<Match>().Select(x => x.Value).ToArray();
        }

        /// <summary>
        /// Function WriteVocab(). Writes the content of a dictionary to a text file.
        /// Used for checking purposes
        /// </summary>
        /// <param name="V">the vocabulary to write to file</param>
        /// <param name="str">the path to the file to write</param>
        static void WriteVocab(Dictionary<string, int> V, string str)
        {
            string file_out = str + "check_vocab.txt";
            System.IO.StreamWriter outfile = new System.IO.StreamWriter(file_out);

            foreach (var token in V)
            {
                outfile.WriteLine("{0},{1}", token.Key, token.Value);
            }

            outfile.Close();

        }

        /// <summary>
        /// Function NBproba(). Calculates the loglikelihood for each token (word) in 
        /// the vocabuary for a given class (e.g. positive or negative) of documents. The
        /// loglikelihoods are stored as key, value pairs in a dictionary where the keys
        /// are the tokens and the values are the loglikelihoods.
        /// The implementation follows the algorithm presented in
        /// [REF:Speech and Language Processing. Daniel Jurafsky and James H. Martin. Copyright 2016.]
        /// The loglikelihood is calculated as follow:
        /// logl[w,c] = (1 + count(w,c in V)) / (sum(count(all w,c in V) + num w in V))
        /// </summary>
        /// <param name="V">The vocabulary for the current cross-validation iteration</param>
        /// <param name="W">The dictionary of common words</param>
        /// <param name="X">The set of docs in the train dataset</param>
        /// <param name="Y">The labels of docs in the train dataset</param>
        /// <param name="driver">The class being analyzed (e.g. positive or negative)</param>
        /// <returns>Dictionary of class likelihoods for each token in the vocabulary,
        /// the logprior probability for the class analyzed</returns>
        public static (Dictionary<string, double>logl, double logp) NBproba(
            Dictionary<string,int> V, Dictionary<string, int> W, List<string> X, List<int> Y, string driver)
        {
            // Xp contains docs for class analyzed
            List<string> Xp = new List<string>();
            // Dictionary of loglikelihoods to return
            Dictionary<string, double> logl = new Dictionary<string, double>();
            // Temporary dictionary
            Dictionary<string, int> vocab_cat = new Dictionary<string, int>();
            // logprior probability to return
            double logp = 0.0;

            // first select docs for class analyzed
            Xp = (driver.Equals("positive") ? SelectReviews(X,Y,1) : SelectReviews(X,Y,0));

            // create a dictionary(key,count) based on docs for class being analyzed 
            CreateVocab(Xp, W, vocab_cat);
            //Console.WriteLine(vocab_cat.Count);

            // get total count of words in class vocabulary.
            // this is neeeded for loglikelihood calculations (first term in the denominator)
            // logl[w,c] = 1 + count(w,c in V) / (sum(count(all w,c in V) + num w in V))
            int count_all_w = 0;
            foreach (var pair in V)
            {
                if (vocab_cat.ContainsKey(pair.Key))
                {
                    // sum(count(all w,c in V)
                    count_all_w += vocab_cat[pair.Key];
                }
            }

            // loop through all the tokens in the vocabulary and calculate 
            // the loglikelihoods. 
            foreach (var pair in V)
            {
                if (vocab_cat.ContainsKey(pair.Key))
                {
                    // logl[w,c]
                    logl.Add(pair.Key,
                        Math.Log((double)(vocab_cat[pair.Key] + 1) / (count_all_w + V.Count)));
                }
                else
                {
                    // this is necessary to take into account words that are present in the 
                    // vocabulary but not in the vocabulary for the class analyzed 
                    logl.Add(pair.Key,
                        Math.Log((double)(1) / (count_all_w + V.Count)));
                }
            }

            // calculate the logprior probability for the class analyzed 
            logp = Math.Log((double)Xp.Count / X.Count);

            // return loglikelihoods and logprior
            return (logl, logp);

        }

        /// <summary>
        /// Function SelectReviews(). Selects all the docs belonging to the class being
        /// analyzed.
        /// </summary>
        /// <param name="myXtrain">The docs to select from</param>
        /// <param name="myYlabel">The labels of those docs</param>
        /// <param name="cat">The class being analyzed (positive or negative)</param>
        /// <returns></returns>
        public static List<string> SelectReviews(List<string> myXtrain, List<int> myYlabel, int cat)
        {
            List<string> list = new List<string>();
            for(int i=0; i<myXtrain.Count; i++)
            {
                if (myYlabel[i] == cat)
                {
                    list.Add(myXtrain[i]);
                }
            }
            return list;
        }

        /// <summary>
        /// Function TestNaiveBayes(). Calculates class posterior probabilities for the test
        /// dataset based on the loglikelihoods and logpriors from the train dataset. Returns 
        /// the overall accuracy for the current cross-validation iteration.
        /// </summary>
        /// <param name="V">The vocabulary for the current cross-validation iteration</param>
        /// <param name="W">The dictionary of common words</param>
        /// <param name="X">The set of docs in the test dataset</param>
        /// <param name="Y">The labels of docs in the test dataset</param>
        /// <param name="loglp">Loglikelihoods for positive class</param>
        /// <param name="logpp">Logprior probability for positive class</param>
        /// <param name="logln">Loglikelihoods for negative class</param>
        /// <param name="logpn">Logprior probability for negative class</param>
        /// <returns>accuracy for current cross-validation iteration</returns>
        public static double TestNaiveBayes(Dictionary<string, int> V,
                                              Dictionary<string, int> W,
                                              List<string> X, List<int> Y,
                                              Dictionary<string, double> loglp, double logpp,
                                              Dictionary<string, double> logln, double logpn)
        {
            // Y is only used to test the accuracy of the model!
            double accuracy = 0.0;
            int match = 0;

            // loop through every doc in test dataset
            for (int itest=0; itest<X.Count; itest++)
            {
                // initialize posterior probabilities to priors probabilities
                double pprob = logpp;
                double nprob = logpn;

                // create vocab for this new review (never seen before)
                Dictionary<string, int> vocab_test = new Dictionary<string, int>();
                string review = X[itest];
                List<string> creview = new List<string>() { review };
                CreateVocab(creview, W, vocab_test);

                foreach (var pair in vocab_test)
                {
                    // if token not present in vocabulary just discard
                    // otherwise add likelihoods to logprior to give the 
                    // posterior probabilities
                    if (V.ContainsKey(pair.Key))
                    {
                        pprob += (pair.Value * loglp[pair.Key]);
                        nprob += (pair.Value * logln[pair.Key]);
                    }
                }

                //Console.WriteLine("positive: {0} and negative: {1}", pprob, nprob);
                // calculate posterior probability - not use - keep logs, becomes too small
                //pprob_ = Math.Exp(pprob) / (Math.Exp(pprob) + Math.Exp(nprob));
                //nprob_ = Math.Exp(nprob) / (Math.Exp(pprob) + Math.Exp(nprob));

                // if posterior probability of positive class greater than
                // posterior probability of negative class than 1 otherwise 0
                // (recall label 1 is positive whilst label 0 is negative)
                int pred = (pprob > nprob ? 1 : 0);
                // check whether prediction matches with label from test dataset.
                match += (pred == Y[itest] ? 1 : 0);
            }
            //Console.WriteLine("{0} {1}", pprob_, nprob_);
            //Console.WriteLine(match);

            // Note: we are using a stratified cross-validation therefore the 
            // classes are balanced. Accuracy = (TP + TN) / (TP + TN + FP + FN) 
            accuracy = ((double)match / Y.Count) * 100;

            // return the accuracy
            return accuracy;
        }

    }
}