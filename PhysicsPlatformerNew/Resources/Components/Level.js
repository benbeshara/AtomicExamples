var game = Atomic.game;
var cache = game.cache;
var scene = game.scene;
var node = self.node;

self.init = function(level) {
        
    node.createJSComponent("Lighting");
    self.tmxFile = cache.getResource("TmxFile2D", "Levels/" + level);
    self.tileMap = node.createComponent("TileMap2D");
    self.tileMap.setTmxFile(self.tmxFile);
    self.levelParser = new LevelParser(self.tileMap);
    
    var backgroundNode = scene.createChild("Background");
    backgroundNode.createJSComponent("Background");

}

function spawnPlayer() {

    var position = self.levelParser.getSpawnpoint();

    self.playerNode = node.createChild("PlayerNode");
    self.player = self.playerNode.createJSComponent("Player");
    self.player.init(position);

}

function createEntities() {

    var platforms = self.levelParser.getEntities("MovingPlatform");
    for (var i = 0; i < platforms.length; i++) {
        var p = platforms[i];
        var pnode = scene.createChild("PlatformNode");
        var platform = pnode.createJSComponent("MovingPlatform");
        platform.init(p.start, p.stop);
    }
    
    var vines = self.levelParser.getEntities("Vine");
    for (var i = 0; i < vines.length; i++) {
        var vnode  = scene.createChild("Vine");
        var vine = vnode.createJSComponent("Vine");
        vine.init(vines[i].position);        
    }
}

function start() {

    // create the physics
    self.levelParser.createPhysics(self.tileMap, self.tmxFile);

    createEntities();

    // spawn the player
    spawnPlayer();

}

function update(timeStep) {

}