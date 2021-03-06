﻿//--------------------------------------------------
// Motion Framework
// Copyright©2018-2020 何冠峰
// Licensed under the MIT license
//--------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MotionFramework.Resource;

namespace MotionFramework.Pool
{
	internal class GameObjectCollector
	{
		private readonly Queue<SpawnGameObject> _collector;
		private readonly List<SpawnGameObject> _loadingSpawn = new List<SpawnGameObject>();
		private readonly Transform _root;
		private AssetReference _assetRef;
		private AssetOperationHandle _handle;
		private GameObject _cloneObject;

		/// <summary>
		/// 对象池容量
		/// </summary>
		public int Capacity { private set; get; }

		/// <summary>
		/// 资源定位地址
		/// </summary>
		public string Location
		{
			get
			{
				return _assetRef.Location;
			}
		}

		/// <summary>
		/// 是否加载完毕
		/// </summary>
		public bool IsDone
		{
			get
			{
				return _handle.IsDone;
			}
		}

		/// <summary>
		/// 当前的加载状态
		/// </summary>
		public EAssetStates States
		{
			get
			{
				return _handle.States;
			}
		}

		/// <summary>
		/// 内部缓存总数
		/// </summary>
		public int Count
		{
			get { return _collector.Count; }
		}

		/// <summary>
		/// 外部使用总数
		/// </summary>
		public int SpawnCount { private set; get; }


		public GameObjectCollector(Transform root, string location, int capacity)
		{
			_root = root;
			Capacity = capacity;

			// 创建缓存池
			_collector = new Queue<SpawnGameObject>(capacity);

			// 加载资源
			_assetRef = new AssetReference(location);
			_handle = _assetRef.LoadAssetAsync<GameObject>();
			_handle.Completed += Handle_Completed;
		}
		private void Handle_Completed(AssetOperationHandle obj)
		{
			_cloneObject = _handle.InstantiateObject;

			// 如果加载失败，创建临时对象
			if (_cloneObject == null)
				_cloneObject = new GameObject(Location);

			// 设置克隆对象
			SetRestoreCloneObject(_cloneObject);

			// 创建初始对象
			for (int i = 0; i < Capacity; i++)
			{
				GameObject cloneObj = GameObject.Instantiate(_cloneObject);
				SpawnGameObject spawn = new SpawnGameObject(this, cloneObj);
				SetRestoreCloneObject(cloneObj);
				_collector.Enqueue(spawn);
			}

			// 最后返回结果
			for (int i = 0; i < _loadingSpawn.Count; i++)
			{
				GameObject cloneObj = GameObject.Instantiate(_cloneObject);
				SpawnGameObject spawn = _loadingSpawn[i];
				spawn.Go = cloneObj;
				if (spawn.IsSpawning)
				{
					SetSpawnCloneObject(cloneObj);
					spawn.UserCallback?.Invoke(cloneObj);
				}
				else
				{
					// 注意：直接回收
					Restore(spawn);
				}
			}
			_loadingSpawn.Clear();
		}

		/// <summary>
		/// 回收游戏对象
		/// </summary>
		public void Restore(SpawnGameObject spawn)
		{
			SpawnCount--;
			spawn.IsSpawning = false;

			// 注意：资源有可能还未加载完毕
			if (spawn.Go != null)
				SetRestoreCloneObject(spawn.Go);

			_collector.Enqueue(spawn);
		}

		/// <summary>
		/// 获取游戏对象
		/// </summary>
		public SpawnGameObject Spawn()
		{
			SpawnGameObject spawn;

			// 如果还未加载完毕
			if (IsDone == false)
			{
				spawn = new SpawnGameObject(this);
				_loadingSpawn.Add(spawn);
			}
			else
			{
				if (_collector.Count > 0)
				{
					spawn = _collector.Dequeue();
					SetSpawnCloneObject(spawn.Go);
				}
				else
				{
					GameObject cloneObj = GameObject.Instantiate(_cloneObject);
					spawn = new SpawnGameObject(this, cloneObj);
					SetSpawnCloneObject(cloneObj);
				}
			}

			SpawnCount++;
			spawn.IsSpawning = true;
			return spawn;
		}

		/// <summary>
		/// 销毁对象池
		/// </summary>
		public void Destroy()
		{
			// 卸载资源对象
			if (_assetRef != null)
			{
				_assetRef.Release();
				_assetRef = null;
			}

			// 销毁游戏对象
			foreach (var item in _collector)
			{
				if(item.Go != null)
					GameObject.Destroy(item.Go);
			}
			_collector.Clear();
			_loadingSpawn.Clear();

			SpawnCount = 0;
		}

		private void SetSpawnCloneObject(GameObject cloneObj)
		{
			cloneObj.SetActive(true);
			cloneObj.transform.parent = null;
			cloneObj.transform.localPosition = Vector3.zero;
		}
		private void SetRestoreCloneObject(GameObject cloneObj)
		{
			cloneObj.SetActive(false);
			cloneObj.transform.SetParent(_root);
			cloneObj.transform.localPosition = Vector3.zero;
		}
	}
}