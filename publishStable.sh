#TODO: increment the minor/major version???
#      maybe a file with a list of pack
readonly srcBranch="master"
readonly dstBranch="master"
readonly srcRepoName="ECSJobDemos"
readonly packagesPath="Samples/Packages/"
readonly srcRepo="https://github.com/Unity-Technologies/$srcRepoName.git"
#readonly dstRepo="https://github.com/Unity-Technologies/EntityComponentSystemSamples.git"
readonly dstRepo="git@gitlab.internal.unity3d.com:core/EntityComponentSystemSamples.git"
#readonly dstRepo="git@gitlab.internal.unity3d.com:fabrizio/ECS.git"

readonly mainPackage="com.unity.entities"

getPackagesFolder()
{
	echo "`dirname $(find . -name manifest.json | grep $packagesPath)`/"
}

#Get the list of packages contained in a folder.
#A package is a directory containing the file package.json
getListOfPackages()
{
	for i in `ls $1 | grep -v manifest.json`
	do
		if [[ -d $1$i && -f "$1$i/package.json" ]]
		then
			echo $i
		fi
	done
}

isPackageChanged()
{
	echo "Fetch latest package..." >&2
	npm install --no-package-lock --prefix . $2  > /dev/null 2>&1
	if [[ $? -eq 0 ]]
	then
		echo "Analyzing differences..." >&2
		diff $1$2 node_modules/$2 -x .git -x package*.json -x .DS_Store -x node_modules -qr
		numChanges=`diff $1$2 node_modules/$2 -x .git -x package*.json -x .DS_Store -x node_modules -qr | wc -l | tr -d ' '`
		rm -r node_modules

		#in some cases, npm creates an etc folder. It it's created i will delete it.
		if [[ -d etc ]]
		then
			rmdir etc
		fi
	else
		numChanges=1
	fi

	if [[ $numChanges -ne 0 ]]
	then
		return 1
	else
		return 0
	fi
}

getPackageCurrentVersion()
{
	local packageVersion
	packageVersion=`npm view $2 version 2> /dev/null`
	if [[ $? -eq 0 ]]
	then
		echo "Version Found: $packageVersion" >&2
		cd $1$2
		npm version $packageVersion  > /dev/null 2>&1
		cd $rootClone
	else
		echo "No version found in repository. Creating a first version..." >&2
		cd $1$2
		npm version 0.0.0  > /dev/null 2>&1
		packageVersion="0.0.0"
		cd $rootClone

	fi

	echo $packageVersion
}

publishNewPackage()
{
	cd $packagesFolder$package
	local packageVersion=`npm version patch | cut -d'v' -f2 2> /dev/null`
	echo "New Version $packageVersion" >&2
	packageArchive=`npm pack .`
	echo "Publishing $packageArchive" >&2
	npm publish $packageArchive > /dev/null 2>&1
	rm $packageArchive
	cd $rootClone
	echo $packageVersion
}

modifyJSON()
{
	# Check if the manifest already contains a declaration of the current package
	grep "$1\"[ \t]*:" $3 > /dev/null 2>&1
	if [[ $? -eq 0 ]]
	then
		#if the package is already declared, change the version declared with the new one.
		if [[ $OSTYPE == *darwin* ]]
		then
			sed -i '' -e 's/\"'"$1"'\"[ \t]*:[ \t]*\"[0-9]\{1,\}\.[0-9]\{1,\}\.[0-9]\{1,\}\"/\"'"$1"'\":\"'"$2"'\"/' $3
		else
			sed -i -e 's/\"'"$1"'\"[ \t]*:[ \t]*\"[0-9]\{1,\}\.[0-9]\{1,\}\.[0-9]\{1,\}\"/\"'"$1"'\":\"'"$2"'\"/' $3
		fi
	else
		#if there are packages already declared, we need to append a comma after the version number
		numPackages=`grep ':[   ]*\"[0-9]' $3 | wc -l | awk '{print $1}'`
		lineTerminator=""
		if [[ $numPackages > 0 ]]
		then
			lineTerminator=","
		fi

		if [[ $OSTYPE == *darwin* ]]
		then
			sed -i '' -e 's/\"dependencies\"[ \t]*:[ \t]*{/\"dependencies\":{\'$'\n\t\t\"'"$1"'\":\"'"$2"'\"'"$lineTerminator"'/' $3
		else
			sed -i -e 's/\"dependencies\"[ \t]*:[ \t]*{/\"dependencies\":{\'$'\n\t\t\"'"$1"'\":\"'"$2"'\"'"$lineTerminator"'/' $3
		fi
	fi
}

scatterManifest()
{
	for i in `find . -name "manifest.json" | grep -v $packagesPath`
	do
		cp "$packagesPath/./manifest.json" "`dirname $i`"
		git add . > /dev/null 2>&1
	done
}

squashCommits()
{
	git fetch stableRepo $dstBranch > /dev/null 2>&1
	if [[ $? -ne 0 ]]
	then
		echo "The stable repo is empty. Creating the first commit..." >&2
		git reset $(git rev-list --max-parents=0 HEAD) > /dev/null 2>&1
		git checkout -b $dstBranch > /dev/null 2>&1
		releaseNumber=1
	else
		git reset stableRepo/$dstBranch > /dev/null 2>&1
		releaseMessage=$(git log --oneline --pretty=format:%s -1)
		releaseNumber=`echo $releaseMessage | cut -d' ' -f2 | awk '{print $1}' 2> /dev/null`
		((releaseNumber++)) 2> /dev/null
		if [[ $? -ne 0 ]]
		then
			releaseNumber=1
		fi
	fi

	git add . > /dev/null 2>&1
	echo $releaseNumber
}

CreateCloneRepo()
{
	if [[ ! -d "../tmp" ]]
	then
		mkdir "../tmp"
		cd "../tmp"
		echo "Cloning repository..." >&2
		git clone $srcRepo
		cd $srcRepoName
		echo "Checking out $srcBranch..." >&2
		git checkout $srcBranch > /dev/null 2>&1
	else
		cd "../tmp/$srcRepoName"
	fi
	echo `pwd`
}

RemoveThisScriptForCommit()
{
	git add .
	git reset -- "`basename $BASH_SOURCE`"
	git reset -- .gitlab-ci.yml
	git rm --cached -r UnstablePrototypes
}

# $1 packages folder
# $2 package to process
# $3 json depender
processPackage()
{
	echo "Package Found: $2"
	local packageVersion=`getPackageCurrentVersion $1 $2`

	isPackageChanged $1 $2
	if [[ $? -eq 1 ]]
	then
		echo "The package repository is not clean. A new version of $1 will be published"
		packageVersion=`publishNewPackage`
	else
		echo "No change detected in the repo. The version $packageVersion will be used in the project"
	fi

	cd $1
	rm -r $2

	modifyJSON $package $packageVersion $3  

	cd $rootClone

	git add .
}

main()
{
	rootDir=`pwd`
	rootClone=`CreateCloneRepo`
	cd $rootClone
	git remote add stableRepo $dstRepo
	git remote remove origin
	releaseNumber=`squashCommits`
	packagesFolder=`getPackagesFolder`
	packages=`getListOfPackages $packagesFolder`

	for package in ${packages[@]}
	do
		if [[ $package != $mainPackage ]]
		then
			processPackage $packagesFolder $package "$mainPackage/package.json"
		fi
	done

	#Handle com.unity.entities (not so much) differently...It just needs to be the last package to be processed....
	if [[ -d $mainPackage ]]
	then
		processPackage $packagesFolder $mainPackage manifest.json
	fi

	scatterManifest > /dev/null 2>&1

	RemoveThisScriptForCommit
	if [[ $releaseNumber -ne 1 ]]
	then
		git commit -m "Release $releaseNumber"
	else
		git commit --amend --reset-author -m "Release $releaseNumber"
	fi
	git push stableRepo $dstBranch
	cd $rootDir
	rm -rf "../tmp"
}

main
