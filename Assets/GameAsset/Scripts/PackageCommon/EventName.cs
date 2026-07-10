using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Wayfu.Lamkn
{
    public static class EventName
    {
	    public const string ShowMiniGame = "ShowMiniGame";
	    public const string ShowMiniMap = "ShowMiniMap";
	    
	    
        public const string ChangeLastSeletedItemShop = "ChangeLastSeletedClothingShop";
        
        public const string NameHouseFriend = "NameHouseFriend";
        public const string ClickUserInfo = "ClickUserInfo";
        public const string ShowOtherInfo = "ShowOtherInfo";





        public static string PauseReceiveInput = "PauseReceiveInput";
        public static string ResumeReceiveInput = "ResumeReceiveInput";
        public static string FIND_FREELOOKCAM = "FIND_FREELOOKCAM";
        public static string ON_MOVE_BY_NAVMESH = "ON_MOVE_BY_NAVMESH";
        public static string LockCamera = "LockCamera"; // lock camera player
        public static string UnlockCamera = "UnlockCamera"; // unlock camera player
        public static string HideUIMyHouse = "HideUIMyHouse"; // unlock camera player
        public static string ShowUIMyHouse = "ShowUIMyHouse"; // unlock camera player
        public static string ShowUIGlobal = "ShowUIGlobal";
        public static string HideUIGlobal = "HideUIGlobal";
        public static string ObjectDeselected = "ObjectDeselected";
        public static string ObjectDeselectedPlayer = "ObjectDeselectedPlayer";
        public static string ObjectSelected = "ObjectSelected";
        public static string PlayerSelected = "PlayerSelected";
        public static string PlayerDeSelected = "PlayerDeSelected";
        public static string BlockAction = "BlockAction";
        public static string UnBlockAction = "UnBlockAction";
        public static string ShowCollapseExpandSub = "ShowCollapseExpandSub";
        public static string RebuildUIGlobal = "RebuildUIGlobal";
        public static string ActiveUIGlobalByKey = "ActiveUIGlobalByKey";
        public static string DeactiveUIGlobalByKey = "DeactiveUIGlobalByKey";
        public static string ItemInteractions = "ItemInteractions";
        public static string ExitItemInteractions = "ExitItemInteractions";

        // chat
        public static string BanChat = "BanChat";
        public static string ReceiveChatGlobal = "ReceiveChatGlobal";
        public static string ReceiveChatPrivate = "ReceiveChatPrivate";
        public static string ReceiveChatRoom = "ReceiveChatRoom";
        public static string ChangeUserStatus = "ChangeUserStatus";
        public static string UserJoinMap = "UserJoinMap";
        public static string ChangeUserInfo = "ChangeUserInfo";
        public static string OpenChatPrivate = "OpenChatPrivate";
        public static string AddMessageChatGlobal = "AddMessageChatGlobal";
        public static string CALL_LIST_FRIEND_CHAT = "CallListFriendChat";
        public static string ClearCacheChat = "ClearCacheChat";
        public static string AddInviteRoomToChatService = "AddInviteRoomToChatService";
        public static string RemoveInviteRoomToChatService = "RemoveInviteRoomToChatService";
        public static string RemoveAllInviteRoomToChatService = "RemoveAllInviteRoomToChatService";

        // friend 
        public static string SelectGiftItem = "SelectGiftItem";
        public static string ClickFriendCellView = "ClickFriendCellView";
        public static string ClickInformation = "ClickInformation";
        
        // receive mobile input
        public static string ReceiveInputChange = "ReceiveInputChange";
        public static string ReceiveInputChangeCamera = "ReceiveInputChangeCamera";
        //public static string StopMovement = "StopMovement";
        public static string DefineInputTarget = "DefineTarget";
        public static string ActiveOtherInfoDetector = "ActiveOtherInfoDetector";

        // sync
        public static string ReceiveSyncData = "ReceiveSyncData";


        public static string OnLoadedScene = "OnLoadedScene";
        public static string OnMovedTargetByNavMesh = "OnMovedTargetByNavMesh";
        public static string MiniMap_GoToPosition = "MiniMap_GoToPosition";

        public static string UpdateMainGameChat = "UpdateMainGameChat";

        public static string NewGift = "NewGift";

        #region Friend Realtime
        public const string RemoveFriend = "RemoveFriend";
        public const string AcceptFriendDone = "AcceptFriendDone";
        public const string RemoveFriendDone = "RemoveFriendDone";
        public const string FriendSendGiftSuccess = "FriendSendGiftSuccess";
        public const string FriendSendGiftError = "FriendSendGiftError";
        public const string FriendSendRequestSuccess = "FriendSendRequest";
        public const string FriendVisisted = "FriendVisisted";
        public const string ItemFriendClickChat = "ItemFriendClickChat";
        public static string OTHERLOGIN = "OTHERLOGIN";
        public static string OtherAcceptAddFriend = "OtherAcceptAddFriend";
        public static string OtherAddFriend = "OtherAddFriend";
        public static string OtherCancelAddFriend = "OtherCancelAddFriend";
        public static string OtherDeclineAddFriend = "OtherDeclineAddFriend";
        public static string OtherUnfriend = "OtherUnfriend";
        public static string SelfUnfriend = "SelfUnfriend";


        public static string NoticeFriendRequest = "NoticeFriendRequest";
        public static string NoticeFriendGift = "NoticeFriendGift";
        public static string NoticeFriendChatPrivate = "NoticeFriendChatPrivate";
        public static string UpdateNoticeChatPrivate = "UpdateNoticeChatPrivate";
        public static string ClearNoticeChat = "ClearNoticeChat";

        #endregion

        #region Avatar Realtime

        public static string RealtimeAvatar = "RealtimeAvatar";
        public static string NoticeReceiveAvatar = "NoticeReceiveAvatar";

        #endregion

        #region Quest

        public static string MaybeCompleteOneQuest = "MaybeCompleteOneQuest";
        public static string HandleUpdateDailyQuestAPI = "HandleUpdateDailyQuestAPI";
        public static string HandleGetHideFunction = "HandleGetHideFunction";
        public static string LoadAgainQuest = "LoadAgainQuest";
        public static string UpdateQuestFromAPI = "UpdateQuestFromAPI";
        public static string BackSceneQuest = "BackSceneQuest";
        public static string OpenSceneQuest = "OpenSceneQuest";

        #endregion

        #region Chat

        public const string ShowInfoPlayerPopup = "ShowInfoPlayerPopup";
        public const string RecivedEmoji = "RecivedEmoji";

        #endregion

        #region Addressable

        public static string PreLoadProgressEvent = "PreLoadProgressEvent";
        public static string PreloadCompletionEvent = "PreloadCompletionEvent";
        public static string PreloadCompletionEventFake = "FakePreloadCompletionEventFake";

        public static string MapLoadDone = "MapLoadDone";

        public static string DownloadAssetsProgressEvent = "DownloadAssetsProgressEvent";
        public static string DownloadImageAssetsCompletionEvent = "DownloadImageAssetsCompletionEvent";
        public static string DownloadAssetsCompletionEventFake = "DownloadAssetsCompletionEventFake";

        public static string StopLoadMap = "StopLoadMap";

        public static string LoadSceneEvent = "LoadSceneEvent";
        public static string LoadSceneEventDone = "LoadSceneEventDone";
        
        public static string DOWNLOAD_ASSETS_DONE = "DOWNLOAD_ASSETS_DONE";
        
        #endregion

        #region HuntAnimal

        public static string HuntAnimalLoadScene = "HuntAnimalLoadScene";

        #endregion

        #region BrainWar

        public static string BrainWar_SelectAnswer = "BrainWar_SelectAnswer";
        public static string BrainWar_AnswerRight = "BrainWar_AnswerRight";
        public static string BrainWar_TooltipDetail = "BrainWar_TooltipDetail";

        #endregion

        #region MY_HOUSE

        public static string CLICK_PLAYER_MYHOUSE = "click_player_myhouse";
        public static string ON_PLACE_ITEM = "on_place_item";
        public static string ON_STORAGE_ITEM = "on_storage_item";
        public static string ON_CANCEL_PREVIEW_ITEM = "on_cancel_preview_item";

        public static string MYHOUSE_START_TUTORIAL = "MYHOUSE_START_TUTORIAL";
        public static string MYHOUSE_END_TUTORIAL = "MYHOUSE_END_TUTORIAL";
        //public static string TUTORIAL_HAND_BUY_HOUSE = "TUTORIAL_HAND_BUY_HOUSE";
        public static string TUTORIAL_BUY_HOUSE = "TUTORIAL_BUY_HOUSE";
        public static string TUTORIAL_OFF_HAND_BUY_HOUSE = "TUTORIAL_OFF_HAND_BUY_HOUSE";
        public static string TUTORIAL_HAND_BACK_BUY_HOUSE = "TUTORIAL_HAND_BACK_BUY_HOUSE";
        public static string BACK_BUYHOUSE = "BACK_BUYHOUSE";
        public static string DONE_BUYHOUSE = "DONE_BUYHOUSE";
        public static string DONE_PICKUP_HOUSE_ITEM = "DONE_PICKUP_HOUSE_ITEM";

        public static string ON_CLICK_GOHOME = "ON_CLICK_GOHOME";
        public static string ON_CLICK_GOOUTSIDE = "ON_CLICK_GOOUTSIDE";
        public static string UI_GLOBAL_MY_HOUSE = "UI_GLOBAL_MY_HOUSE";
        public static string UI_GLOBAL_TUTORIAL = "UI_GLOBAL_TUTORIAL";
        public static string UI_GLOBAL_TUTORIAL_START = "UI_GLOBAL_TUTORIAL_START";
        public static string UI_GLOBAL_END_TUTORIAL = "UI_GLOBAL_END_TUTORIAL";
        public static string UI_GLOBAL_TUTORIAL_INVENTORY = "UI_GLOBAL_TUTORIAL_INVENTORY";
        public static string UI_GLOBAL_TUTORIAL_BACK_INVENTORY = "UI_GLOBAL_TUTORIAL_BACK_INVENTORY";
        public static string UI_GLOBAL_TUTORIAL_HAND_INVENTORY = "UI_GLOBAL_TUTORIAL_HAND_INVENTORY";
        public static string UI_GLOBAL_TUTORIAL_OFF_HAND_INVENTORY = "UI_GLOBAL_TUTORIAL_OFF_HAND_INVENTORY";
        public static string UI_GLOBAL_TUTORIAL_BACK_MINIGAME = "UI_GLOBAL_TUTORIAL_BACK_MINIGAME";
        public static string UI_GLOBAL_TUTORIAL_SHOP = "UI_GLOBAL_TUTORIAL_SHOP";
        public static string UI_GLOBAL_HAND_SHOP = "UI_GLOBAL_HAND_SHOP";
        public static string UI_GLOBAL_TUTORIAL_BACK_SHOP = "UI_GLOBAL_TUTORIAL_BACK_SHOP";
        public static string UI_GLOBAL_TUTORIAL_QUEST = "UI_GLOBAL_TUTORIAL_QUEST";

        public static string ON_HIDE_UI_MYHOUSE = "ON_HIDE_UI_MYHOUSE";
        public static string ON_BUILD_HOUSE = "ON_BUILD_HOUSE";

        public static string ON_TRIGGER_DOOR = "ON_TRIGGER_DOOR";

        public static string INIT_PLAYER_AND_CAMERA = "INIT_PLAYER_AND_CAMERA";
        
        #endregion

        #region TUTORIAL
        public static string BACK_INVENTORY = "BACK_INVENTORY";
        public static string CLICK_INVENTORY = "CLICK_INVENTORY";
        public static string HANDLE_CLICK_ITEM_INVENTORY = "HANDLE_CLICK_ITEM_INVENTORY";
        public static string CLICK_ITEM_INVENTORY = "CLICK_ITEM_INVENTORY";
        public static string HANDLE_CLICK_BACK_INVENTORY = "HANDLE_CLICK_BACK_INVENTORY";
        public static string DONE_MINIGAME = "DONE_MINIGAME";
        public static string SHOW_REWARD = "SHOW_REWARD";
        public static string BACK_SHOP = "BACK_SHOP";
        public static string UI_SELECT_MINIGAME = "UI_SELECT_MINIGAME";
        public static string BLOCK_SPAWN = "BLOCK_SPAWN";
        
        //Event tutorial
        public static string START_TUTORIAL_EVENT = "START_TUTORIAL_EVENT";
        public static string END_TUTORIAL_DROP = "END_TUTORIAL_DROP";
        public static string END_TUTORIAL_EVENT = "END_TUTORIAL_EVENT";
        public static string HANDLE_CLICK_EVENT = "HANDLE_CLICK_EVENT";
        public static string HANDLE_BACK_EVENT = "HANDLE_BACK_EVENT";
        public static string OPEN_GO_TUTORIAL_EVENT = "OPEN_GO_TUTORIAL_EVENT";
        public static string CLICK_ITEM_TUTORIAL_EVENT = "CLICK_ITEM_TUTORIAL_EVENT";
        public static string TUTORIAL_EVENT_ANSWER = "TUTORIAL_EVENT_ANSWER";
        public static string ANSWER_TUTORIAL_DONE = "ANSWER_TUTORIAL_DONE";
        public static string HANDLE_END_TUTORIAL_ERROR = "HANDLE_END_TUTORIAL_ERROR";
        public static string HANDLE_END_TUTORIAL_ERROR_UIGLOBAL = "HANDLE_END_TUTORIAL_ERROR_UIGLOBAL";






        #endregion

        #region PET
        public static string ON_HIDE_PET = "ON_HIDE_PET";
        public static string ON_USE_PET_ITEM = "ON_USE_PET_ITEM";
        public static string RELOAD_STATUS_PET = "RELOAD_STATUS_PET";
        public static string RELOAD_PET_STAMINA = "RELOAD_PET_STAMINA";
        public static string CLOSE_POPUP_PET = "CLOSE_POPUP_PET";
        public static string SHOW_POPUP_INTERACT_PET = "SHOW_POPUP_INTERACT_PET";
        public static string HIDE_POPUP_INTERACT_PET = "HIDE_POPUP_INTERACT_PET";

        #endregion

        #region SHOP
        public static string Shop_ClickTabClothesSet = "Shop_ClickTabClothesSet";
        public static string ON_CLICK_FURNITURE_ITEM = "on_click_furniture_item";
        public static string ON_SHOW_PREVIEW_ITEM = "on_show_preview_item";
        public static string ON_CLICK_ITEM_SHOP = "ClickItemShop";
        public static string ON_RELOAD_ITEM_SHOP = "OnReloadItemShop";
        public static string ON_CHANGE_STATE_ITEM_SHOP = "ChangeStateItemShop";
        public static string ON_BOUGHT_ITEM = "ON_BOUGHT_ITEM";
        public static string ON_INVENTORY_SELECT_VEHICLE = "ON_INVENTORY_SELECT_VEHICLE";
        #endregion

        #region Inventory

		public const string OnClickBonusItem = "OnClickBonusItem";
		public const string OnClickInvenoryItem = "OnClickInvenoryItem";
        public const string UpdateRewardItemsInInventory = "UpdateRewardItemsInInventory";
        public const string ReloadScrollerInventory = "ReloadScrollerInventory";

        public const string TweenPlayerInventory = "TweenPlayerInventory";


        #endregion

        public static string EventDTTT = "EventDTTT";
        public static string Event_UpdatePlayCount = "Event_UpdatePlayCount";
        public static string RewardAchievement = "RewardAchievement";
        public static string Level_Up = "LevelUp";
        public static string ChangeUserTitle = "ChangeUserTitle";

        #region Notice

        public const string NoticeInvite = "NoticeInvite";

		// chỉ đếm những tin nhắn realtime, không áp dụng tin nhắn lưu api
		public const string NoticeChatPrivate = "NoticeChatPrivate";
		public const string NoticeChatGlobal = "NoticeChatGlobal";

		public const string ReceiveInviteRoom = "ReceiveInviteRoom";
		public const string UpdateNoticeDotRed = "UpdateNoticeDotRed";
		public const string UpdateNoticeDotRedOffline = "UpdateNoticeDotRedOffline";
		public const string UpdateNumberWheelTicket = "UpdateNumberWheelTicket";

		#endregion

		public static string OpenNoticePopup = "OpenNoticePopup";
		public static string ReturnToLogin = "ReturnToLogin";
		public static string SHOW_UP_COMING_POPUP = "SHOW_UP_COMING_POPUP";
		public static string SHOW_NOTIFICATION_POPUP = "SHOW_NOTIFICATION_POPUP";
		public static string ShowPopupNewsMessageOffline = "ShowPopupNewsMessageOffline";
		public static string CHECK_UNREAD_NOTIFICATION_POPUP = "CHECK_UNREAD_NOTIFICATION_POPUP";
		// public static string SHOW_TOURNAMENT_NOTIFICATION_POPUP = "SHOW_TOURNAMENT_NOTIFICATION_POPUP";
		public static string REAL_TIME_CHECKING_VERSION = "REAL_TIME_CHECKING_VERSION";
		public static string REQUEST_LOAD_SCENE_EVENT="REQUEST_LOAD_SCENE_EVENT";


        #region EVENT MAIN

        public static string LoadEventItem = "LoadEventItem";
		public static string SelectEventItem = "SelectEventItem";
		public static string NotifyEvent = "NotifyEvent";
		public static string SpawnLetterGiftBox = "SpawnLetterGiftBox";
		public static string ShowPopupEnoughTicket = "ShowPopupEnoughTicket";

		#endregion

		#region MINI GAME

		public static string OnClickSportItem = "OnClickSportItem";
		public static string LoadLastScreen = "LoadLastScreen";
		public static string OnToggleChanged = "OnToggleChanged";
		public static string ClickTitle= "ClickTitle";

        #endregion

        #region REFER

        public static string OnClickReferItem = "OnClickReferItem";
		public static string OnClickReferAddItem = "OnClickReferAddItem";
		public static string UpdateAcumulatePoint = "UpdateAcumulatePoint";
		public static string OnClickAccumulateDetail = "OnClickAccumulateDetail";
		public static string OnClickEnterCode = "OnClickEnterCode";

		#endregion

		#region Reddot

		public static string SeenNoticeNewDevice { get; set; } = "SeenNoticeNewDevice";

		public static string UpdateShoppingItemsCallGetConfigLogin { get; set; } =
			"UpdateShoppingItemsCallGetConfigLogin";
		
		#endregion

		#region IAP

		public static string ShowInfoItemIAP = "ShowInfoItem";
		public static string BuyItemIAPDone = "BuyItemIAPDone";

		#endregion

		#region PROFILE

		public static string EquipTitlePicked = "EquipTitlePicked";
		public static string ChangeName = "ChangeName";
		public static string ChangeIdMyHouseHardCodeClientOffline = "ChangeIdMyHouseHardCodeClientOffline";

        public static string LoadDataAchivement = "LoadDataAchivement";
        public static string LoadDataAvatarFrame = "LoadDataAvatarFrame";

        #endregion

        #region Login


        //#region Login
        //public static string LoginNullUserName = "NullUserName";
        //public static string LoginStrengthUserName = "LoginStrengthUserName";
        //public static string MAP_SELECTION = "map selection";
        //public static string QUICK_LOGIN = "quick login";

        public static string ON_OPEN_SELECT_MAP = "ON_OPEN_SELECT_MAP";
        public static string ON_CLOSE_SELECT_MAP = "ON_CLOSE_SELECT_MAP";
        public static string ON_SHOW_CREATE_USER = "ON_SHOW_CREATE_USER";
        public static string ON_LOAD_MAP = "ON_LOAD_MAP";
        public static string ON_CLICK_REGISTER_POPUP = "ON_CLICK_REGISTER_POPUP";
        public static string ON_SHOW_LOGIN = "ON_SHOW_LOGIN";
        public static string HANDLE_CONNECT_TCP = "ON_DONE_GET_LOGIN";
        public static string ON_CONNECTED_TCP = "ON_CONNECTED_TCP";
        public static string ON_MAINTACE_REGISTER = "ON_MAINTACE_REGISTER";

        #endregion
        
        #region SETTING

        public static string ON_CHANGE_PASS_SUCCESS = "ON_CHANGE_PASS_SUCCESS";
        public static string ON_DELETE_ACCOUNT_SUCCESS = "ON_DELETE_ACCOUNT_SUCCESS";
        public static string ON_CLOSE_DELETE_ACCOUNT = "ON_CLOSE_DELETE_ACCOUNT";
        public static string ON_SHOW_DELETE_ACCOUNT = "ON_SHOW_DELETE_ACCOUNT";

        #endregion

        #region SOCKET

        public static string ON_LOGOUT_GAME = "ON_LOGOUT_GAME";
        public static string UDP = "UDP";

        #endregion

        #region Internet
        public static string HAS_INTERNET = "HAS_INTERNET";
        public static string NO_INTERNET = "NO_INTERNET";

        #endregion

        #region POPUP

        public static string HIDE_POPUP = "HIDE_POPUP";
        public static string HIDE_POPUP_RATE = "HIDE_POPUP_RATE";

        #endregion

        #region MINI GAME DONT FALL

        public static string OnClickCellTopic = "OnClickCellTopic";
        // public static string OnClickRanking = "OnClickRanking";
        // public static string OnClickReplay = "OnClickReplay";
        // public static string OnClickExit = "OnClickExit";

        #endregion
        #region REMOTE_CONFIG
		public static string HANDLE_CONFIG_TCP = "SET STATUS REMOTE CONFIG";

        #endregion

		 #region Player

		 // public static string SPAWN_PLAYER = "SPAWN_PLAYER";
		 // public static string DESPAWN_PLAYER = "DESPAWN_PLAYER";
		 public static string ON_PLAYER_MOVE_TO_NPC = "ON_PLAYER_MOVE";
		 public static string ON_PLAYER_MOVE_TO_POSITION = "ON_PLAYER_MOVE_TO_POSITION";
        
         public	static string ON_PLAYER_WEAR_VEHICLE = "ON_PLAYER_WEAR_VEHICLE";
         public	static string ON_PLAYER_IN_HOME_VEHICLE = "ON_PLAYER_IN_HOME_VEHICLE";
         public	static string ON_PLAYER_OUT_HOME_VEHICLE = "ON_PLAYER_OUT_HOME_VEHICLE";
         public	static string ON_PLAYER_IN_CHAIR_VEHICLE = "ON_PLAYER_IN_CHAIR_VEHICLE";
         public	static string ON_PLAYER_EXIT_CHAIR_VEHICLE = "ON_PLAYER_EXIT_CHAIR_VEHICLE";

        #endregion

        #region LEARNING

        //Learning
        public static string BACK_CITY_MAP_FROM_LEARNG = "BACK_CITY_MAP_FROM_LEARNG";
        public static string ON_OPEN_LEARNING = "ON_OPEN_LEARNING";
        public static string ON_OPEN_SUBJECT = "ON_OPEN_SUBJECT";
        public static string ON_OPEN_UNIT = "ON_OPEN_UNIT";
        public static string ON_OPEN_GRADE = "ON_OPEN_GRADE";
        public static string ON_OPEN_ACTIVITY = "ON_OPEN_ACTIVITY";
        public static string ON_BACK_GRADE = "ON_BACK_GRADE";
        //public static string ON_FILTER_GRADE = "ON_FILTER_GRADE";
        public static string ON_UNIT_PREVIEW_GIFT = "ON_UNIT_PREVIEW_GIFT";
        public static string ON_ACTIVITY_PREVIEW_GIFT = "ON_ACTIVITY_PREVIEW_GIFT";
        public static string ON_OPEN_QUIZ = "ON_OPEN_QUIZ";
        public static string ON_RELOAD_UNIT_SCREEN = "ON_RELOAD_UNIT_SCREEN";
        public static string OPEN_BACKGROUND_QUIZ = "OPEN_BACKGROUND_QUIZ";
        public static string CLOSE_BACKGROUND_QUIZ = "CLOSE_BACKGROUND_QUIZ";
        public static string OPEN_LEARN_CASE_ERROR = "OPEN_LEARN_CASE_ERROR";

        //Quiz
        public static string ON_NEXT_QUESTION = "ON_NEXT_QUESTION";
        public static string ON_SINGLE_CHOICE_ANSWER = "ON_SINGLE_CHOICE_ANSWER";
        public static string ON_SINGLE_CHOICE_ANSWER_REMOVE = "ON_SINGLE_CHOICE_ANSWER_REMOVE";
        public static string ON_MULTIPLE_CHOICE_ANSWER = "ON_MULTIPLE_CHOICE_ANSWER";
        public static string ON_PLAY_AUDIO_SINGLE = "ON_PLAY_AUDIO_SINGLE";
        //Quiz Reading
        public static string ON_SINGLE_CHOICE_ANSWER_READING = "ON_SINGLE_CHOICE_ANSWER_READING";
        public static string ON_SINGLE_CHOICE_ANSWER_REMOVE_READING = "ON_SINGLE_CHOICE_ANSWER_REMOVE_READING";
        public static string ON_MULTIPLE_CHOICE_ANSWER_READING = "ON_MULTIPLE_CHOICE_ANSWER_READING";
        //End 
        public static string ON_ADD_SUBMIT_MODEL = "ON_ADD_SUBMIT_MODEL";
        public static string ON_REMOVE_SUBMIT_MODEL = "ON_REMOVE_SUBMIT_MODEL";
		public static string ON_QUIZ_LEAVE_SUBMIT = "ON_QUIZ_LEAVE_SUBMIT";
		
		public static string ON_QUIZ_PAUSE_AUDIO = "ON_QUIZ_PAUSE_AUDIO";
		public static string ON_QUIZ_PAUSE_VIDEO = "ON_QUIZ_PAUSE_VIDEO";
		public static string ON_QUIZ_UNPAUSE_VIDEO = "ON_QUIZ_UNPAUSE_VIDEO";
        public static string ON_QUIZ_UNPAUSE_AUDIO = "ON_QUIZ_UNPAUSE_AUDIO";
		
		public static string ON_COUNT_DOWN_DONE = "ON_COUNT_DOWN_DONE";
		public static string ON_DOWNLOAD_VIDEO_PROGRESS = "ON_DOWNLOAD_VIDEO_PROGRESS";
		public static string ON_DOWNLOAD_VIDEO_PROGRESS_CAN_CANCEL = "ON_DOWNLOAD_VIDEO_PROGRESS_CAN_CANCEL";
		public static string ON_SKIP_DOWNLOAD_VIDEO_PROGRESS = "ON_SKIP_DOWNLOAD_VIDEO_PROGRESS";
		public static string OPEN_GROUP_BUTTON = "OPEN_GROUP_BUTTON";
		public static string CLOSE_GROUP_BUTTON = "CLOSE_GROUP_BUTTON";
		public static string NEXT_OR_FINISH_QUIZ_LEANING = "NEXT_OR_FINISH_QUIZ_LEANING";
		public static string SET_LANGUAGE_BACK_SPEAKING = "SET_LANGUAGE_BACK_SPEAKING";
		
		// SKIP MICROPHONE QUIZ
		public static string ON_NO_MICROPHONE_SKIP = "ON_NO_MICROPHONE_SKIP";
		public static string ON_NO_MICROPHONE_QUIT = "ON_NO_MICROPHONE_QUIT";

        #endregion

        #region PARENT
        public static string SHOW_AGE_ON_PARENT = "SHOW_AGE_ON_PARENT";
        //public static string ON_CHANGE_GLOBAL_CHAT = "ON_CHANGE_GLOBAL_CHAT";
        //public static string ON_CHANGE_PRIVATE_CHAT = "ON_CHANGE_PRIVATE_CHAT";
        public static string SET_TIME_LEFT = "SET_TIME_LEFT";
        public static string ON_CONFIRM_SET_TIME_LEFT = "ON_CONFIRM_SET_TIME_LEFT";
        public static string ON_CLICK_CONFIRM_EXTEND = "ON_CLICK_CONFIRM_EXTEND";
        public static string SET_UI_CLOCK_UIGLOBAL = "SET_UI_CLOCK_UIGLOBAL";
        public static string ON_CHANGE_EMAIL_SUCCEESS = "ON_CHANGE_EMAIL_SUCCEESS";
        #endregion


        #region Realtime friend

        public const string AcceptRequestAddFriend = "AcceptRequestAddFriend";
        public const string DeclineRequestAddFriend = "DeclineRequestAddFriend";
        public const string CancelRequestAddFriend = "CancelRequestAddFriend";
        public const string Unfriend = "Unfriend";
        public const string FriendSendGift = "FriendSendGift";
        public const string NewRequestAddFriend = "NewRequestAddFriend";


        #endregion
        #region MiniGame
        #endregion
        #region Multiplayer
        public static string ON_OPEN_MULTIPLAYER_LOBBY = "ON_OPEN_MULTIPLAYER_LOBBY";
        public static string ON_USER_JOIN_ROOM = "ON_USER_JOIN_ROOM";
        public static string ON_OTHER_JOIN_ROOM = "ON_OTHER_JOIN_ROOM";
        public static string ON_PLAYER_LEAVE_ROOM = "ON_PLAYER_LEAVE_ROOM";
        public static string ON_PLAYER_BE_KICK = "ON_PLAYER_BE_KICK";
        #endregion Multiplayer
        #region Navigation line
        public const string ENABLE_LINE_NAVIGATION = "SHOW_LINE_NAVIGATION";
        public const string DISABLE_LINE_NAVIGATION = "DISABLE_LINE_NAVIGATION";
        public const string StopMoveByInputUser = "StopMoveByInputUser";
        public const string EnableButtonTurnOffNavigationLine = "EnableButtonTurnOffNavigationLine";
        public const string DisableButtonTurnOffNavigationLine = "DisableButtonTurnOffNavigationLine";
        public const string StopMoveByInput = "StopMoveByInput";
        
        // tắt quay camera khi dừng navmesh
        public const string StopCameraRotationNavmesh = "StopCameraRotationNavmesh";


        #endregion
        #region IAP
        public const string CHECK_MEMBERSHIP = "CheckMembership";
        public const string ON_SHOW_CREATE_NAME = "OnShowCreateName";
        public const string ON_SHOW_FILL_PIN = "OnShowFillPin";
        public const string ON_FILL_PIN_DONE = "OnFillPinDone";
        public const string ON_VERIFYBUY_DONE = "OnVerifyBuyDone";
        public const string ON_APPLY_VOUCHER_DONE = "OnApplyVoucherDone";
        public const string ON_ADMIN_ADD_VOUCHER = "OnAdminAddVoucher";
        #endregion


        #region Home Buying
        public const string ChangeHome = "ChangeHome";
        public const string OpenInsideHomeRaise = "OpenInsideHomeRaise";
        #endregion
        #region TypingRaceMultiplayer
        public static string ON_USER_JOIN_ROOM_TYPINGRACE = "ON_USER_JOIN_ROOM_TYPINGRACE";
        #endregion
        #region HintPopup
        public static string UI_GLOBAL_HINT_QUEST = "UI_GLOBAL_HINT_QUEST";
        public static string UI_GLOBAL_HINT_LEARNING = "UI_GLOBAL_HINT_LEARNING";
        public static string UI_GLOBAL_HINT_AREA = "UI_GLOBAL_HINT_AREA";
        public static string UI_GLOBAL_HINT_HOME = "UI_GLOBAL_HINT_HOME";
        public static string UI_GLOBAL_HINT_GAME = "UI_GLOBAL_HINT_GAME";
        public static string UI_GLOBAL_HINT_STORY = "UI_GLOBAL_HINT_STORY";
        public static string UI_GLOBAL_HINT_OFF_CANVAS = "UI_GLOBAL_HINT_OFF_CANVAS";
        public static string TURN_OFF_HINT_POPUP = "TURN_OFF_HINT_POPUP";
        public static string RESET_TIME_HINT = "RESET_TIME_HINT";
        #endregion

        #region MATH RUN

        
        public static string ON_PLAYER_SPAWN = "ON_PLAYER_SPAWN";
        public static string END_GAME_MATHRUN = "END_GAME_MATHRUN";

        #endregion
        #region BUILD HOUSE

        public static string ON_OFF_MOBILE_INPUT = "ON_OFF_MOBILE_INPUT";
        public static string ON_SHOW_UI_BUILD_HOUSE = "ON_SHOW_UI_BUILD_HOUSE";
        public static string ON_HIDE_UI_BUILD_HOUSE = "ON_HIDE_UI_BUILD_HOUSE";
        public static string ON_INTERACT_ITEM_BUILD_HOUSE = "ON_INTERACT_ITEM_BUILD_HOUSE";
        public static string OFF_INTERACT_ITEM_BUILD_HOUSE = "OFF_INTERACT_ITEM_BUILD_HOUSE";

        #endregion

        #region  ModeOnlyLearn

        public static string UI_MODE_ONLY_LEARN_SHOW_UI = "UI_MODE_ONLY_LEARN_SHOW_UI";

        #endregion
        #region  LearningPlanningTime

        public static string Set_Learning_Time_Today = "Set_Learning_Time_Today";

        #endregion

        public static string CodeFunGameRunTimeError = "CodeFunGameRunTimeError";


        #region Story Event Record
        public const string Story_Event_Record_Update_LikeCount = "Story_Event_Record_Update_LikeCount";
        public const string Story_Event_Record_Update_ListeningCount = "Story_Event_Record_Update_ListeningCount";
        #endregion

        #region AiAssistant

        public static string UPDATE_INDOOR_ITEM = "UPDATE_INDOOR_ITEM";
        public static string UPDATE_OUTDOOR_ITEM = "UPDATE_OUTDOOR_ITEM";
        public static string UPDATE_PET = "UPDATE_PET";
        public static string WELCOME = "WELCOME";
        public static string NEW_FEATURES = "NEW_FEATURES";
        public static string NEW_EVENTS = "NEW_EVENTS";
        public static string NEW_SHOPPING_ITEMS = "NEW_SHOPPING_ITEMS";
        public static string NEW_GACHA_ITEMS = "NEW_GACHA_ITEMS";
        public static string NEW_COLLECTION_ITEMS = "NEW_COLLECTION_ITEMS";
        public static string EVENT_GIFT_NOT_EXCHANGED = "EVENT_GIFT_NOT_EXCHANGED";
        public static string DAILY_QUEST_NOT_COMPLETED = "DAILY_QUEST_NOT_COMPLETED";
        public static string AI_ASSISTANT_LOGIN = "AI_ASSISTANT_LOGIN";

        #endregion

        #region AutoCheckIn

        public static string AutoCheckIn = "AutoCheckIn";

        #endregion
    }
}