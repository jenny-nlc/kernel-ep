function [ s] = l_clutterTrainMsgs( nload, op )
%L_CLUTTERTRAINMSGS Load training messages for clutter problem.
% n = number of instances to load
% 
if nargin < 2
    op = [];
end

seed = myProcessOptions(op, 'seed', 1);

% path to load training set
clutter_data_path = myProcessOptions(op, 'clutter_data_path', ...
    'saved/clutterTrainMsgs.mat');
oldRng = rng;
rng(seed);

% fpath = 'saved/clutterTrainMsgs.mat';

assert(exist(clutter_data_path, 'file')~=0 );
load(clutter_data_path);
% load() will load 'n', 'op', 'a', 'w', 'X', 'T', 'Xout', 'Tout'

% use remove. So we can keep the sorted inputs.
toremove = length(X)-min(nload, length(X));
Id = randperm( length(X),  toremove);
X(Id) = [];
T(Id) = [];
Xout(Id) = [];
Tout(Id) = [];

% Learn operator with cross validation
% In = tensor of X and T
% XIns = ArrayInstances(X);
XIns = MV1Instances(X);
% TIns = ArrayInstances(T);
TIns = MV1Instances(T);
In = TensorInstances({XIns, TIns});

% pack everything into a struct s
s.op = op;
s.a = a;
s.w = w;
s.X = X;
s.T = T;
s.Xout = Xout;
s.Tout = Tout;

s.XIns = XIns;
s.TIns = TIns;
s.In = In;

rng(oldRng);
end

