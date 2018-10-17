#!/usr/bin/env bash

source ./CONFIG.inc

VERSIONFILE=$PACKAGE.version

scp -i $SSH_ID ./GameData/$PACKAGE/$VERSIONFILE $SITE:/$TARGET_CONTENT_PATH
scp -i $SSH_ID ./GameData/$PACKAGE/README.md $SITE:/$TARGET_CMS_PATH/$PACKAGE.md
scp -i $SSH_ID -rp "./PR_material/${PACKAGE}" $SITE:/${TARGET_CONTENT_PATH}PR_material/
