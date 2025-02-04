﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Windows;
using Server.Tools;
//using System.Windows.Controls;
//using System.Windows.Documents;
//using System.Windows.Ink;
//using System.Windows.Input;
//using System.Windows.Shapes;

namespace HSGameEngine.Tools.AStarEx
{
    public class AStar
    {
        private BinaryStack _open;
        private Dictionary<long, bool> _closed;
		private NodeGrid _grid;
		private int _endNodeX;
        private int _endNodeY;
		private int _startNodeX;
        private int _startNodeY;
		private List<ANode> _path;
		private double _straightCost = 1.0;
		private double _diagCost = 1.4142135623730951;
        public const double costMultiplier = 1.0;

        //open count 内部有60个点需要检测，那这条路肯定很难寻,返回失败就行
        //gm命令 -modifyastar 100可以修改
        public static int MaxOpenNodeCount = 200;
		
		public AStar()
		{
		}

        //查寻路径
        public List<ANode> find(NodeGrid grid)
        {
            if (findPath(grid))
            {
                //这儿返回的path不要移除第一个元素，客户端移除了，但是服务器端原来没有移除，所以，这儿也不要移除
                return _path;
            }

            return null;
        }
		
		public bool findPath(NodeGrid grid)
		{
			_grid = grid;
            if (null == _open)
            {
                _open = new BinaryStack("f");
            }
            else
            {
                _open.ClearAll();
            }

            grid.Clear();
            _open._nodeGrid = grid; //每次寻路， 必须设置，内部使用

            if (null == _closed)
            {
                _closed = new Dictionary<long, bool>(1000);
            }
            else
            {
                _closed.Clear();
            }
			
			_startNodeX = _grid.startNodeX;
            _startNodeY = _grid.startNodeY;
			_endNodeX = _grid.endNodeX;
            _endNodeY = _grid.endNodeY;
			
            _grid.Nodes[_startNodeX, _startNodeY].g = 0;
            _grid.Nodes[_startNodeX, _startNodeY].h = diagonal(_startNodeX, _startNodeY);
            _grid.Nodes[_startNodeX, _startNodeY].f = _grid.Nodes[_startNodeX, _startNodeY].g + _grid.Nodes[_startNodeX, _startNodeY].h;

			return search();
		}
		
		public bool search()
		{

            try
            {
                //异步运算。当上一次遍历超出最大允许值后停止遍历，下一次从
                //上次暂停处开始继续遍历		
                long node = ANode.GetGUID(_startNodeX, _startNodeY);
                long endNode = ANode.GetGUID(_endNodeX, _endNodeY);

                int nodex = 0;
                int nodey = 0;

                while (node != endNode)
                {
                    nodex = ANode.GetGUID_X(node);
                    nodey = ANode.GetGUID_Y(node);

                    int startX = 0 > nodex - 1 ? 0 : nodex - 1;
                    int endX = _grid.numCols - 1 < nodex + 1 ? _grid.numCols - 1 : nodex + 1;
                    int startY = 0 > nodey - 1 ? 0 : nodey - 1;
                    int endY = _grid.numRows - 1 < nodey + 1 ? _grid.numRows - 1 : nodey + 1;

                    for (int i = startX; i <= endX; i++)
                    {
                        for (int j = startY; j <= endY; j++)
                        {
                            //待检测的点太多，没必要再寻路了
                            if (_open.getLength() > MaxOpenNodeCount)
                            {
                                LogManager.WriteLog(LogTypes.Warning, String.Format("AStar:search()待检测的点太多，没必要再寻路: start({0}, {1}), to({2}, {3}), MaxOpenNodeCount={4}",
                                    _startNodeX, _startNodeY, _endNodeX, _endNodeY, MaxOpenNodeCount));
                                return false;
                            }

                            long test = ANode.GetGUID(i, j);

                            int testx = i;
                            int testy = j;

                            bool isTestWalkable = _grid.isWalkable(testx, testy);
                            if (test == node || !isTestWalkable ||
                                !_grid.isDiagonalWalkable(node, test))
                            {
                                continue;
                            }

                            double cost = _straightCost;

                            if (!((nodex == testx) || (nodey == testy)))
                            {
                                cost = _diagCost;
                            }

                            double nodeg = _grid.Nodes[nodex, nodey].g;
                            double g = nodeg + cost * costMultiplier;
                            double h = diagonal(testx, testy);
                            double f = g + h;

                            Boolean isInOpen = _open.indexOf(test) != -1;
                            int indexOfClose = IndexOfClose(test);

                            if (isInOpen || indexOfClose != -1)
                            {
                                if (_grid.Nodes[testx, testy].f > f)
                                {
                                    _grid.Nodes[testx, testy].f = f;
                                    _grid.Nodes[testx, testy].g = g;
                                    _grid.Nodes[testx, testy].h = h;
                                    _grid.Nodes[testx, testy].parentX = nodex;
                                    _grid.Nodes[testx, testy].parentY = nodey;

                                    if (isInOpen)
                                        _open.updateNode(test);
                                }
                            }
                            else
                            {
                                _grid.Nodes[testx, testy].f = f;
                                _grid.Nodes[testx, testy].g = g;
                                _grid.Nodes[testx, testy].h = h;
                                _grid.Nodes[testx, testy].parentX = nodex;
                                _grid.Nodes[testx, testy].parentY = nodey;
                                _open.push(test);
                            }

                        }
                    }

                    _closed[node] = true;
                    if (_open.getLength() == 0)
                    {
                        return false;
                    }

                    node = _open.shift();
                }

                //所有的node，都由很多副本，所以，很多时候，必须生成确定的副本
                nodex = ANode.GetGUID_X(node);
                nodey = ANode.GetGUID_Y(node);

                _endNodeX = nodex;
                _endNodeY = nodey;

                buildPath();
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
            }
			
			return true;
        }

        /// <summary>
        /// 判断是否在close列表中
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private int IndexOfClose(long node)
        {
            return _closed.ContainsKey(node) ? 0 : -1;
        }

		private void buildPath()
		{
			_path = new List<ANode>();
            ANode node = new ANode(_endNodeX, _endNodeY);
            _path.Add(node);

            int count = 0;
            while (!(node.x == _startNodeX && node.y == _startNodeY))
			{
                int px = _grid.Nodes[node.x, node.y].parentX;
                int py = _grid.Nodes[node.x, node.y].parentY;
                node = new ANode(px, py);
                _path.Insert(0, node);
                count++;
			}

            System.Diagnostics.Debug.WriteLine(string.Format("Find Path count={0}", count));
		}
		
		/** 判断两个节点的对角线路线是否可走 */
		private bool isDiagonalWalkable( long node1, long node2 )
		{
            return _grid.isDiagonalWalkable(node1, node2);
            //if (node1.walkable && node2.walkable)
            //{
            //    return true;
            //}

            //return false;
		}
		
		private double diagonal(int nodex, int nodey)
		{
			double dx = nodex - _endNodeX < 0 ? _endNodeX - nodex : nodex - _endNodeX;
			double dy = nodey - _endNodeY < 0 ? _endNodeY - nodey : nodey - _endNodeY;
			double diag = dx < dy ? dx : dy;
			double straight = dx + dy;
			return _diagCost * diag + _straightCost * (straight - 2 * diag);
		}
		
	//---------------------------------------get/set functions-----------------------------//
		
		public List<ANode> path
		{
            get { return _path; }
		}
    }
}
