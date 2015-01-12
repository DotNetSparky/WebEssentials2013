using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using Microsoft.Build.Framework;
using Newtonsoft.Json;
using Directory = Pri.LongPath.Directory;
using DirectoryInfo = Pri.LongPath.DirectoryInfo;
using File = Pri.LongPath.File;
using FileInfo = Pri.LongPath.FileInfo;
using Path = Pri.LongPath.Path;

namespace MadsKristensen.NodeServiceInstaller
{
	public class NodePackage
	{
		readonly NodeModule _rootModule;
		readonly string _rootPath;

		private readonly List<string> _concernedScripts = new List<string>()
		{
			"preinstall", "install", "postinstall", "preuninstall", "uninstall", "postuninstall"
		};

		public NodeModule Root
		{
			get
			{
				return _rootModule;
			}
		}

		public bool ChangesMade { get; set; }

		LogWrapper Log { get; set; }

		public NodePackage(string name, string rootPath, LogWrapper log)
		{
			_rootModule = new NodeModule(name, null);
			_rootPath = rootPath;
			Log = log;
		}

		public void OptimizeModules(IList<string> ignorePaths)
		{
			Root.CalculateWeights(_rootPath, ignorePaths);

			Log.LogMessage("");
			Log.LogMessage("Looking for script hooks:");
			foreach (NodeModule i in Root.WalkBreadthFirst().Where(x => x.Scripts.Count > 0))
			{
				bool has = false;
				foreach (KeyValuePair<string,string> s in i.Scripts)
				{
					if (_concernedScripts.Contains(s.Key))
					{
						if (!has)
						{
							Log.LogMessage(i + ":");
							has = true;
						}
						Log.LogMessage("  " + s.Key + " = \"" + s.Value + "\"");
					}
				}
			}
			Log.LogMessage("");

			Log.LogMessage("");
			Log.LogMessage("Original Module List:");
			PrintNodeTree();

			int originalMaxWeight = Root.WalkDepthFirst().Select(x => x.FullWeight).Max();

			// try to move the heaviest module up
			//  -- if that can't be moved, try each parent
			// repeat until nothing can be changed

		startOver:
			foreach (NodeModule iLeaf in Root.WalkDepthFirst().Where(x => !x.Any()).OrderByDescending(x => x.FullWeight))
			{
				foreach (NodeModule obj in iLeaf.Ancestors().OrderByDescending(x => x.Weight).Where(x => x.Parent != null && x.Parent.Parent != null))
				{
					// try to move up one level
					NodeModule targetParent = obj.Parent.Parent;
					NodeModule conflict = targetParent.Scope().FirstOrDefault(x => NodeModule.EqualsName(x, obj));
					if (conflict != null)
					{
						if (NodeModule.EqualsVersion(obj, conflict))
						{
							// there's already a module with the same version higher in scope, so this one can be removed
							Log.LogMessage("Removing: " + obj);
							Log.LogMessage("  (duped by: " + conflict + ")");
							RemoveModule(obj);
							goto startOver;
						}
						if (conflict.Parent == targetParent)
							continue;
					}
					// is elligible to move if all of the nodes dependencies either:
					//    (a) exist at the new scope
					//    or (b) exist as a child of obj (even if removed--we'll unremove it)
					if (obj.IsElligibleToMove(targetParent))
					{
						Log.LogMessage("Moving up: " + obj);
						MoveModule(obj, targetParent);
						goto startOver;
					}
				}
			}

			CleanNodeTree();

			int newMaxWeight = Root.WalkDepthFirst().Select(x => x.FullWeight).Max();
			Log.LogMessage("Original Max weight: " + originalMaxWeight);
			Log.LogMessage("New Max weight: " + newMaxWeight);

			Log.LogMessage("");
			Log.LogMessage("New Module List:");

			PrintNodeTree();
		}

		void CleanNodeTree()
		{
			// purge any items that are marked both "Add" and "Remove"
			foreach (NodeModule i in Root.WalkBreadthFirst())
			{
				int c = 0;
				while (c < i.Children.Count)
				{
					NodeModule iObj = i.Children[c];
					if (iObj.Add && iObj.Remove)
						i.Children.Remove(iObj);
					else
						c++;
				}
			}
		}

		void RemoveModule(NodeModule module)
		{
			if (module.Add)
				module.Parent.Children.Remove(module);
			else
				module.MarkRemoved();
		}

		void MoveModule(NodeModule module, NodeModule newParent)
		{
			if (module.Parent != newParent)
			{
				NodeModule newModule = null;
				List<NodeModule> oldDependencies = null;

				// make a copy and put it in the new parent
				if (!newParent.Children.Contains(module.Name))
				{
					newModule = newParent.NewChildFromCopy(module);
					newModule.Add = true;
					oldDependencies = module.FindDependencies().ToList();
				}

				RemoveModule(module);
				if (newModule == null)
					return;

				foreach (NodeModule iOldDep in oldDependencies)
				{
					NodeModule iNewDep = newModule.Scope().FirstOrDefault(x => NodeModule.EqualsVersion(x, iOldDep));
					if (iNewDep == null)
					{
						iNewDep = newModule.Children.Find(iOldDep.Name);
						if (iNewDep != null && iNewDep.Remove)
							iNewDep.Remove = false;
					}
				}
			}
		}

		void PrintNodeTree()
		{
			Log.LogMessage("");
			Root.SortChildren();
			PrintNodeTree(Root);
			Log.LogMessage("");
		}

		void PrintNodeTree(NodeModule node)
		{
			if (node.Remove)
				return;

			string action = node.Remove ? "-" : (node.Add ? "+" : " ");
			Log.LogMessage(action + " " + node);
			if (node.Remove)
			{
				NodeModule replacement = node.FirstNonDeletedParent().Scope().FirstOrDefault(x => NodeModule.EqualsVersion(x, node));
				if (replacement == null)
					Log.LogMessage("  >> ** replacement not found above **");
				else
					Log.LogMessage("  >> replaced by: " + replacement);
			}
			else
			{
				if (node.Dependencies.Count > 0)
				{
					Log.LogMessage("  >> depends on: " + string.Join(", ", node.Dependencies));
					foreach (VersionDependency i in node.Dependencies)
					{
						NodeModule dep = node.FindDependencies().FirstOrDefault(x => NodeModule.EqualsName(x, i.Name));
						if (dep != null)
							Log.LogMessage("    >> " + i + " --> " + dep);
						else
							Log.LogMessage("    >> " + i + " --> *** MISSING ***");
					}
				}
			}
			List<NodeModule> dependants = node.FindDependants().ToList();
			if (dependants.Count > 0)
			{
				Log.LogMessage("  >> depended upon by: ");
				foreach (NodeModule i in dependants)
				{
					Log.LogMessage("    >> " + i);
				}
			}
			foreach (NodeModule i in node.Children)
			{
				PrintNodeTree(i);
			}
		}
	}
}
