﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NetMining.ClusteringAlgo;
using NetMining.ExtensionMethods;
namespace NetMining.Graphs
{
    public class Tenacity : IClusteringAlgorithm
    {
        private readonly bool[] _removedNodes;
        private LightWeightGraph g;
        private List<int> _nodeRemovalOrder;
        private bool _performedHillClimb = false;
        private int _numNodesRemoved;

        private double _minVat = double.MaxValue;//This will hold the best score so far
        public readonly double Alpha, Beta;

        public List<int> NodeRemovalOrder
        {
            get { return new List<int>(_nodeRemovalOrder); }
        }

        public int NumNodesRemoved
        {
            get { return _numNodesRemoved; }
        }

        public double MinVat
        {
            get { return _minVat; }
        }

        private readonly bool _reassignNodes;
        //Vat computes given a graph
        public Tenacity(LightWeightGraph lwg, bool reassignNodes = true, double alpha = 1.0f, double beta = 0.0f)
        {

            //set our alpha and beta variables
            Alpha = alpha; Beta = beta;

            //first we set our variables up
            _removedNodes = new bool[lwg.NumNodes];
            _nodeRemovalOrder = new List<int>();
            _reassignNodes = reassignNodes;

            //We will make a copy of the graph and set the label equal to the index
            g = new LightWeightGraph(lwg, _removedNodes);
            for (int i = 0; i < g.NumNodes; i++)
                g.Nodes[i].Label = i;

            if (lwg.NumNodes <= 2)
                return;

            bool threaded = Settings.Threading.ThreadHVAT;
            //This is where our estimate for Vat is calculated
            for (int n = 0; n < g.NumNodes - 1; n++)  // was g.NumNodes / 2
            {
                //get the graph
                LightWeightGraph gItter = new LightWeightGraph(g, _removedNodes);
                //sw.Restart();
                //get the betweeness
                double[] betweeness = (threaded) ? BetweenessCentrality.ParallelBrandesBcNodes(gItter) :
                    BetweenessCentrality.BrandesBcNodes(gItter);
                //sw.Stop();
                //Console.WriteLine("{0} {1}ms", n+1, sw.ElapsedMilliseconds);
                //get the index of the maximum
                int indexMaxBetweeness = betweeness.IndexOfMax();
                int labelOfMax = gItter.Nodes[indexMaxBetweeness].Label;

                //now we should add it to our list 
                _nodeRemovalOrder.Add(labelOfMax);
                _removedNodes[labelOfMax] = true;
                //calculate vat and update the record
                double vat = CalculateTenacity(_removedNodes);
                if (vat < _minVat)
                {
                    _minVat = vat;
                    _numNodesRemoved = n + 1;
                }
            }

            //Now we need to set up S to reflect the actual minimum
            for (int i = 0; i < _removedNodes.Length; i++)
                _removedNodes[i] = false;
            for (int i = 0; i < _numNodesRemoved; i++)
                _removedNodes[_nodeRemovalOrder[i]] = true;
        }

        public Tenacity(LightWeightGraph lwg, bool reassignNodes = true, double alpha = 1.0f, double beta = 0.0f, List<int> nodeRemovalOrder = null, int numNodesRemoved = 0)
        {

            //set our alpha and beta variables
            Alpha = alpha; Beta = beta;

            //first we set our variables up
            _removedNodes = new bool[lwg.NumNodes];
            _nodeRemovalOrder = nodeRemovalOrder;
            _numNodesRemoved = numNodesRemoved;
            _reassignNodes = reassignNodes;

            //We will make a copy of the graph and set the label equal to the index
            g = new LightWeightGraph(lwg, _removedNodes);
            for (int i = 0; i < g.NumNodes; i++)
                g.Nodes[i].Label = i;

            if (lwg.NumNodes <= 2)
                return;

            //bool threaded = Settings.Threading.ThreadHVAT;
            //This is where our estimate for Vat is calculated
            for (int n = 0; n < g.NumNodes; n++)  // This was evaluating g.Numnodes/2
            {
                //get the graph
                LightWeightGraph gItter = new LightWeightGraph(g, _removedNodes);
                //sw.Restart();
                //get the betweeness
                //double[] betweeness = (threaded) ? BetweenessCentrality.ParallelBrandesBcNodes(gItter) :
                //    BetweenessCentrality.BrandesBcNodes(gItter);
                //sw.Stop();
                //Console.WriteLine("{0} {1}ms", n+1, sw.ElapsedMilliseconds);
                //get the index of the maximum
                //int indexMaxBetweeness = betweeness.IndexOfMax();
                //int labelOfMax = gItter.Nodes[indexMaxBetweeness].Label;

                //now we should add it to our list 
                int labelOfMax = _nodeRemovalOrder[n];
                //_nodeRemovalOrder.Add(labelOfMax);
                _removedNodes[labelOfMax] = true;
                //calculate vat and update the record
                double vat = CalculateTenacity(_removedNodes);
                if (vat < _minVat)
                {
                    _minVat = vat;
                    _numNodesRemoved = n + 1;
                }
            }

            //Now we need to set up S to reflect the actual minimum
            for (int i = 0; i < _removedNodes.Length; i++)
                _removedNodes[i] = false;
            for (int i = 0; i < _numNodesRemoved; i++)
                _removedNodes[_nodeRemovalOrder[i]] = true;
        }

        

        public LightWeightGraph GetAttackedGraph()
        {
            return new LightWeightGraph(g, _removedNodes);
        }

        //Clean up with GetComponents
        public LightWeightGraph GetAttackedGraphWithReassignment()
        {
            LightWeightGraph.LightWeightNode[] nodes = new LightWeightGraph.LightWeightNode[g.NumNodes];


            //get the connectivity structure
            List<int>[] edges = new List<int>[g.NumNodes];
            List<double>[] edgeWeights = new List<double>[g.NumNodes];

            for (int i = 0; i < g.NumNodes; i++)
            {
                List<int> edgeList = new List<int>();
                List<double> weightList = new List<double>();
                edges[i] = edgeList;
                edgeWeights[i] = weightList;
            }

            //Now do a BFS to figure out what each node belongs to
            int[] componentIndex = new int[g.NumNodes];

            //This will provide our visited flags for BFS
            bool[] isVisited = (bool[])_removedNodes.Clone();

            int componentId = 1;
            Queue<int> q = new Queue<int>();
            for (int i = 0; i < g.NumNodes; i++)
            {
                if (!isVisited[i])
                {
                    //BFS to count the size of the component
                    q.Enqueue(i);
                    isVisited[i] = true;
                    while (q.Count > 0)
                    {
                        int v = q.Dequeue();
                        componentIndex[v] = componentId;
                        foreach (int u in g.Nodes[v].Edge)
                            if (!isVisited[u])
                            {
                                q.Enqueue(u);
                                isVisited[u] = true;
                            }
                    }
                    componentId++;
                }
            }

            //Assign the nodes to a component based on degree count
            for (int i = 0; i < NumNodesRemoved; i++)
            {
                int n = _nodeRemovalOrder[i];
                int[] componentDegreeCount = new int[componentId];
                for (int e = 0; e < g.Nodes[n].Edge.Count(); e++)
                {
                    int adjacentNode = g.Nodes[n].Edge[e];
                    componentDegreeCount[componentIndex[adjacentNode]]++;
                }

                //Now we must pick the biggest
                int comp = 1;
                for (int c = 1; c < componentDegreeCount.Length; c++)
                {
                    if (componentDegreeCount[c] > componentDegreeCount[comp])
                        comp = c;
                }
                componentIndex[n] = comp;
            }

            //Now that we have the components, we can build our edge list
            for (int v = 0; v < g.NumNodes; v++)
            {
                LightWeightGraph.LightWeightNode n = g.Nodes[v];
                for (int e = 0; e < n.Edge.Count(); e++)
                {
                    int edge = n.Edge[e];
                    //If they are in the same component, we can add the edge safely
                    if (componentIndex[v] == componentIndex[edge])
                    {
                        edges[v].Add(edge);
                        if (g.IsWeighted)
                            edgeWeights[v].Add(n.EdgeWeights[e]);
                    }
                }
            }

            for (int i = 0; i < nodes.Length; i++)
            {
                nodes[i] = new LightWeightGraph.LightWeightNode(i, g.IsWeighted, edges[i], edgeWeights[i]);
            }

            return new LightWeightGraph(nodes, g.IsWeighted);
        }

        public Partition GetPartition()
        {
            LightWeightGraph lwg = (_reassignNodes) ? GetAttackedGraphWithReassignment() : GetAttackedGraph();

            //Get our cluster Assignment
            List<List<int>> componentList = lwg.GetComponents();

            //Setup our Clusters
            List<Cluster> clusterList = new List<Cluster>();
            for (int i = 0; i < componentList.Count; i++)
            {
                Cluster c = new Cluster(i);
                foreach (var n in componentList[i])
                {
                    c.AddPoint(new ClusteredItem(lwg[n].Label));
                }
                clusterList.Add(c);
            }

            String meta = "Tenacity: \nRemoved Count:" + NumNodesRemoved + "\n"
                          + String.Join(",", _nodeRemovalOrder.GetRange(0, NumNodesRemoved));

            return new Partition(clusterList, g, meta);
        }

        //Use GetComponents
        private double CalculateTenacity(bool[] s)
        {
            //We must get the size of S
            bool[] sClone = (bool[])s.Clone();
            int sizeS = s.Count(c => c);

            if (sizeS == 0)
                return double.MaxValue;

            //find the maximum sized component in the attacked graph
            var components = g.GetComponents(previsitedList: sClone);

            if (components.Count == 1 || components.Count == 0)
                return double.MaxValue;

            int cMax = components.Select(c => c.Count).Max();

            //calculate tenacity = |S|+cmax/#connectedComponents
            return (((double)sizeS + cMax) / components.Count);
        }

        /// <summary>
        /// Perform 1-d HillClimb to locally optimize VAT.  This will change NodeRemovalOrder to hold only the nodes
        /// contained in the new optimal calculation
        /// </summary>
        //public void HillClimb()
        public void HillClimbOrig()
        {
            if (_performedHillClimb)
                return;
            int i = 0;
            double bestVAT = _minVat;
            bool[] s = (bool[])_removedNodes.Clone();

            while (i < g.NumNodes)
            {
                //flip a bit and calculate
                s[i] ^= true;
                double vat = CalculateTenacity(s);
                if (vat < bestVAT)
                {
                    bestVAT = vat;
                    i = 0;
                    continue;
                }
                //if the result is not an improvement, so reset the bit and increment i
                s[i++] ^= true;
            }

            //Set our new results
            _minVat = bestVAT;
            _nodeRemovalOrder = new List<int>();
            for (i = 0; i < g.NumNodes; i++)
            {
                _removedNodes[i] = s[i];
                if (s[i]) _nodeRemovalOrder.Add(i);
            }
            _numNodesRemoved = _removedNodes.Count(c => c);
            _performedHillClimb = true;
        }


        public void HillClimb2()
        //public void HillClimb()
        {
            if (_performedHillClimb)
                return;
            //int i = 0;
            double bestVAT = _minVat;
            bool[] s = (bool[])_removedNodes.Clone();
            bool changed = true;
            int bestFlippedBit = -1;

            while (changed)
            {
                bestFlippedBit = -1;
                changed = false;
                for (int i = 0; i < g.NumNodes; i++)
                {
                    //flip a bit and calculate

                    s[i] ^= true;
                    double vat = CalculateTenacity(s);
                    if (vat < bestVAT)
                    {
                        bestVAT = vat;
                        bestFlippedBit = i;
                        //changed = true;
                    }
                    // unflip the bit
                    s[i] ^= true;
                    //if the result is not an improvement, so reset the bit and increment i
                    //s[i++] ^= true;
                }
                // after going through the entire list, flip the most successful flip
                if (bestFlippedBit >= 0)
                {
                    s[bestFlippedBit] ^= true;
                    changed = true;
                }
            }
            //Set our new results
            _minVat = bestVAT;
            _nodeRemovalOrder = new List<int>();
            for (int i = 0; i < g.NumNodes; i++)
            {
                _removedNodes[i] = s[i];
                if (s[i]) _nodeRemovalOrder.Add(i);
            }
            _numNodesRemoved = _removedNodes.Count(c => c);
            _performedHillClimb = true;
        }



        //public void HillClimb2D()
        public void HillClimb()
        {
            if (_performedHillClimb)
                return;
            //int i = 0;
            double bestVAT = _minVat;
            bool[] s = (bool[])_removedNodes.Clone();
            bool changed = true;
            int bestFlippedBit = -1;
            int bestFlippedBit2 = -1;
            int count=0;
            // first do a 1D hillclimbing
            while (changed && count < 10)
            {
                bestFlippedBit = -1;
                changed = false;
                for (int i = 0; i < g.NumNodes; i++)
                {
                    //flip a bit and calculate

                    s[i] ^= true;
                    double vat = CalculateTenacity(s);
                    if (vat < bestVAT)
                    {
                        bestVAT = vat;
                        bestFlippedBit = i;
                        //changed = true;
                    }
                    // unflip the bit
                    s[i] ^= true;
                    //if the result is not an improvement, so reset the bit and increment i
                    //s[i++] ^= true;
                }
                // after going through the entire list, flip the most successful flip
                if (bestFlippedBit >= 0)
                {
                    s[bestFlippedBit] ^= true;
                    changed = true;
                    count++;
                }
            }


            //Second is a 2D hillclimbing
            changed = true;
            while (changed)
            {
                bestFlippedBit = -1;
                bestFlippedBit2 = -1;
                changed = false;
                for (int i = 0; i < g.NumNodes; i++)
                {
                    for (int j = 1; j < g.NumNodes && j != i; j++)
                    {
                        //flip a bit and calculate
                        s[i] ^= true;
                        s[j] ^= true;
                        double vat = CalculateTenacity(s);
                        if (vat < bestVAT)
                        {
                            bestVAT = vat;
                            bestFlippedBit = i;
                            bestFlippedBit2 = j;
                            //changed = true;
                        }
                        // unflip the bit
                        s[i] ^= true;
                        s[j] ^= true;
                        //if the result is not an improvement, so reset the bit and increment i
                        //s[i++] ^= true;
                    }
                }
                // after going through the entire list, flip the most successful flip
                if (bestFlippedBit >= 0)
                {
                    s[bestFlippedBit] ^= true;
                    s[bestFlippedBit2] ^= true;
                    changed = true;
                }
            }
            //Set our new results
            _minVat = bestVAT;
            _nodeRemovalOrder = new List<int>();
            for (int i = 0; i < g.NumNodes; i++)
            {
                _removedNodes[i] = s[i];
                if (s[i]) _nodeRemovalOrder.Add(i);
            }
            _numNodesRemoved = _removedNodes.Count(c => c);
            _performedHillClimb = true;
        }
    }
}
