# UnityChanSpringBone Burst
UnityChan Spring Bone System for lightweight secondary animations.

This repository was customized using JobSystem/Burst Compiler.

This is a fork tweaked for Fire Emblem Engage modding.

# Changelog

## 1.0.8
- Fixed an issue that prevented changes from properly reflected in the Spring Job Manager.
- Added back a debug view of the Spring Job Manager cache (SortedBones, jobColliders, etc.) to help troubleshoot configuration issues.

What follows is the original README.

## How to set up (example)

- [SunnySideUp UnityChan](https://unity-chan.com/contents/news/3878/) のプロジェクトデータをダウンロードしてください
![image](https://user-images.githubusercontent.com/57246289/90631247-5854a680-e25d-11ea-8ab8-be3147681e29.png)
- **Packages/UnityChanSpringBone-release-1.1** を削除します
![image](https://user-images.githubusercontent.com/57246289/90631366-981b8e00-e25d-11ea-80b0-c28adacea6b4.png)
- 本リポジトリをダウンロードして、**Packages**ディレクトリ以下に展開してください
![image](https://user-images.githubusercontent.com/57246289/90633807-aff51100-e261-11ea-9e3d-ff898adaba33.png)
![image](https://user-images.githubusercontent.com/57246289/90633717-7f14dc00-e261-11ea-9b77-9b1cc085cd39.png)

## How to use

- SpringManagerを持つGameObject（rootノード可）を選択
- **UTJ/選択したSpringBoneをJob化** を選択、実行

![image](https://user-images.githubusercontent.com/57246289/90634062-1a0db600-e262-11ea-998a-c7f239a09ef0.png)


## Attention!

- Job化したSpringBone、SpringColliderを再編集することは出来ません
- Job化した後に元に戻せるよう対応しました。ただし旧データを保持するために若干のメモリ確保量が増加します。



## Required

Unity 2019.4 LTS
Burst Compiler v1.3.6 (verified)



## License

UnityChanSpringBone

Copyright (c) 2018 Unity Technologies

Code released under the MIT License

https://opensource.org/licenses/mit-license.php

//----------------------------------------------------------------------------

NativeContainerPool.cs

Copyright (c) 2020 Yugo Fujioka

Code released under the MIT License

https://opensource.org/licenses/mit-license.php

//----------------------------------------------------------------------------

TaskSystem.cs

Copyright (c) 2017 Yugo Fujioka

Code released under the MIT License

https://opensource.org/licenses/mit-license.php
